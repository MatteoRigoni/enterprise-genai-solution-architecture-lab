using AiSa.Application;
using AiSa.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;

namespace AiSa.Infrastructure;

/// <summary>
/// PostgreSQL + pgvector implementation of IVectorStore (ADR-0003).
/// </summary>
public class PgVectorVectorStore : IVectorStore
{
    private const int VectorDimensions = 1536;
    private const string TableName = "aisa_chunks";
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    private readonly NpgsqlDataSource _dataSource;
    private readonly PgVectorOptions _options;
    private readonly ILogger<PgVectorVectorStore> _logger;
    private bool _initialized;

    public PgVectorVectorStore(
        IOptions<PgVectorOptions> options,
        IConfiguration configuration,
        ILogger<PgVectorVectorStore> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var connectionString = GetConnectionString(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("PgVector connection string is required. Set VectorStore:PgVector:ConnectionString or Host/Port/Database/User and PgVector__Password (env).", nameof(options));

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseVector();
        _dataSource = builder.Build();
    }

    public async Task AddDocumentsAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        var list = chunks.ToList();
        if (list.Count == 0) return;

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.IndexTimeoutSeconds));

        var sourceIds = list.Select(c => c.SourceId).Distinct().ToList();
        _logger.LogInformation(
            "Adding {ChunkCount} chunks. SourceIds: {SourceIds}",
            list.Count,
            string.Join(", ", sourceIds));

        await using var conn = await _dataSource.OpenConnectionAsync(timeoutCts.Token).ConfigureAwait(false);
        foreach (var chunk in list)
        {
            await using var cmd = new NpgsqlCommand(
                $"INSERT INTO {TableName} (chunk_id, chunk_index, content, embedding, source_id, source_name, indexed_at) " +
                "VALUES ($1, $2, $3, $4, $5, $6, $7) " +
                "ON CONFLICT (chunk_id) DO UPDATE SET chunk_index = $2, content = $3, embedding = $4, source_id = $5, source_name = $6, indexed_at = $7",
                conn);
            cmd.Parameters.AddWithValue(chunk.ChunkId);
            cmd.Parameters.AddWithValue(chunk.ChunkIndex);
            cmd.Parameters.AddWithValue(chunk.Content);
            cmd.Parameters.AddWithValue(new Vector(chunk.Vector));
            cmd.Parameters.AddWithValue(chunk.SourceId);
            cmd.Parameters.AddWithValue(chunk.SourceName);
            cmd.Parameters.AddWithValue(chunk.IndexedAt);
            await cmd.ExecuteNonQueryAsync(timeoutCts.Token).ConfigureAwait(false);
        }

        _logger.LogInformation("Indexed {ChunkCount} chunks for sources: {SourceIds}", list.Count, string.Join(", ", sourceIds));
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(float[] queryVector, int topK, CancellationToken cancellationToken = default)
    {
        if (queryVector == null || queryVector.Length == 0)
            throw new ArgumentException("Query vector cannot be null or empty", nameof(queryVector));
        if (topK <= 0)
            throw new ArgumentException("topK must be greater than 0", nameof(topK));

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.SearchTimeoutSeconds));

        _logger.LogInformation("Searching with vector dimension {Dim}, topK: {TopK}", queryVector.Length, topK);

        var results = new List<SearchResult>();
        await using var conn = await _dataSource.OpenConnectionAsync(timeoutCts.Token).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            $"SELECT chunk_id, chunk_index, content, source_id, source_name, indexed_at, embedding <=> $1 AS distance " +
            $"FROM {TableName} ORDER BY embedding <=> $1 LIMIT $2",
            conn);
        cmd.Parameters.AddWithValue(new Vector(queryVector));
        cmd.Parameters.AddWithValue(topK);

        await using (var reader = await cmd.ExecuteReaderAsync(timeoutCts.Token).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(timeoutCts.Token).ConfigureAwait(false))
            {
                var chunk = new DocumentChunk
                {
                    ChunkId = reader.GetString(0),
                    ChunkIndex = reader.GetInt32(1),
                    Content = reader.GetString(2),
                    Vector = Array.Empty<float>(),
                    SourceId = reader.GetString(3),
                    SourceName = reader.GetString(4),
                    IndexedAt = reader.GetFieldValue<DateTimeOffset>(5)
                };
                var distance = reader.GetDouble(6);
                var score = 1.0 - (distance / 2.0);
                results.Add(new SearchResult { Chunk = chunk, Score = score });
            }
        }

        _logger.LogInformation("Search returned {Count} results", results.Count);
        return results.OrderByDescending(r => r.Score);
    }

    public async Task DeleteBySourceIdAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("SourceId cannot be null or empty", nameof(sourceId));

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Deleting chunks for sourceId: {SourceId}", sourceId);

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand($"DELETE FROM {TableName} WHERE source_id = $1", conn);
        cmd.Parameters.AddWithValue(sourceId);
        var deleted = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Deleted {Count} chunks for sourceId: {SourceId}", deleted, sourceId);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;
        await InitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized) return;
            await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            await using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", conn))
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            conn.ReloadTypes();

            await using (var cmd = new NpgsqlCommand($@"
                CREATE TABLE IF NOT EXISTS {TableName} (
                    chunk_id TEXT PRIMARY KEY,
                    chunk_index INTEGER NOT NULL,
                    content TEXT NOT NULL,
                    embedding vector({VectorDimensions}) NOT NULL,
                    source_id TEXT NOT NULL,
                    source_name TEXT NOT NULL,
                    indexed_at TIMESTAMPTZ NOT NULL
                )", conn))
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await using (var cmd = new NpgsqlCommand(
                $"CREATE INDEX IF NOT EXISTS idx_{TableName}_embedding ON {TableName} USING hnsw (embedding vector_cosine_ops)",
                conn))
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            _initialized = true;
            _logger.LogInformation("PgVector table and index ready: {Table}", TableName);
        }
        finally
        {
            InitLock.Release();
        }
    }

    private string GetConnectionString(IConfiguration configuration)
    {
        // When running under Aspire AppHost, connection is injected as ConnectionStrings:pgvector
        var aspireConnectionString = configuration["ConnectionStrings:pgvector"];
        if (!string.IsNullOrWhiteSpace(aspireConnectionString))
            return aspireConnectionString;

        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            var pwd = configuration["PgVector:Password"] ?? Environment.GetEnvironmentVariable("PgVector__Password");
            if (!string.IsNullOrEmpty(pwd))
            {
                var cs = new NpgsqlConnectionStringBuilder(_options.ConnectionString) { Password = pwd };
                return cs.ConnectionString;
            }
            return _options.ConnectionString;
        }
        var password = configuration["PgVector:Password"] ?? Environment.GetEnvironmentVariable("PgVector__Password");
        if (string.IsNullOrEmpty(password))
            return string.Empty;
        return new NpgsqlConnectionStringBuilder
        {
            Host = _options.Host,
            Port = _options.Port,
            Database = _options.Database,
            Username = _options.User,
            Password = password
        }.ConnectionString;
    }
}
