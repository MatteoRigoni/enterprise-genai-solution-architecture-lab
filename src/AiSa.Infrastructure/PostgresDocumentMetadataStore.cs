using AiSa.Application;
using AiSa.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AiSa.Infrastructure;

/// <summary>
/// PostgreSQL implementation of <see cref="IDocumentMetadataStore"/>.
/// Persists document lifecycle metadata to survive application restarts.
/// </summary>
public class PostgresDocumentMetadataStore : IDocumentMetadataStore
{
    private const string TableName = "aisa_document_metadata";
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresDocumentMetadataStore> _logger;
    private bool _initialized;

    public PostgresDocumentMetadataStore(
        IOptions<PgVectorOptions> options,
        IConfiguration configuration,
        ILogger<PostgresDocumentMetadataStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var connectionString = GetConnectionString(options?.Value, configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Postgres metadata store requires a valid connection string.");
        }

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _dataSource = builder.Build();
    }

    public async Task StoreAsync(IngestionResult result)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var normalizedSourceName = NormalizeSourceName(result.SourceNameNormalized ?? result.SourceName);

        await using var conn = await _dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);

        // Retrieve and lock latest active version for this source name
        DocumentMetadata? existingLatest = null;
        await using (var latestCmd = new NpgsqlCommand($"""
            SELECT document_id, source_name, source_name_normalized, chunk_count, indexed_at, status, version, previous_version_id, is_deprecated, content_hash
            FROM {TableName}
            WHERE source_name_normalized = $1 AND is_deprecated = FALSE
            ORDER BY version DESC
            LIMIT 1
            FOR UPDATE
            """, conn, tx))
        {
            latestCmd.Parameters.AddWithValue(normalizedSourceName);
            await using var reader = await latestCmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (await reader.ReadAsync().ConfigureAwait(false))
            {
                existingLatest = MapRow(reader);
            }
        }

        var version = existingLatest?.Version + 1 ?? 1;
        var previousVersionId = existingLatest?.DocumentId;

        if (!string.IsNullOrWhiteSpace(previousVersionId))
        {
            await using var depCmd = new NpgsqlCommand(
                $"UPDATE {TableName} SET is_deprecated = TRUE WHERE document_id = $1",
                conn,
                tx);
            depCmd.Parameters.AddWithValue(previousVersionId);
            await depCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (var insertCmd = new NpgsqlCommand($"""
            INSERT INTO {TableName}
            (document_id, source_name, source_name_normalized, chunk_count, indexed_at, status, version, previous_version_id, is_deprecated, content_hash)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, FALSE, $9)
            ON CONFLICT (document_id)
            DO UPDATE SET
                source_name = EXCLUDED.source_name,
                source_name_normalized = EXCLUDED.source_name_normalized,
                chunk_count = EXCLUDED.chunk_count,
                indexed_at = EXCLUDED.indexed_at,
                status = EXCLUDED.status,
                version = EXCLUDED.version,
                previous_version_id = EXCLUDED.previous_version_id,
                is_deprecated = EXCLUDED.is_deprecated,
                content_hash = EXCLUDED.content_hash
            """, conn, tx))
        {
            insertCmd.Parameters.AddWithValue(result.SourceId);
            insertCmd.Parameters.AddWithValue(result.SourceName);
            insertCmd.Parameters.AddWithValue(normalizedSourceName);
            insertCmd.Parameters.AddWithValue(result.ChunkCount);
            insertCmd.Parameters.AddWithValue(result.CompletedAt);
            insertCmd.Parameters.AddWithValue(result.Status.ToString());
            insertCmd.Parameters.AddWithValue(version);
            insertCmd.Parameters.AddWithValue((object?)previousVersionId ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue((object?)result.ContentHash ?? DBNull.Value);
            await insertCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await tx.CommitAsync().ConfigureAwait(false);
    }

    public async Task<IEnumerable<DocumentMetadata>> GetAllAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var list = new List<DocumentMetadata>();

        await using var conn = await _dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand($"""
            SELECT document_id, source_name, source_name_normalized, chunk_count, indexed_at, status, version, previous_version_id, is_deprecated, content_hash
            FROM {TableName}
            WHERE is_deprecated = FALSE
            ORDER BY indexed_at DESC
            """, conn);

        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(MapRow(reader));
        }

        return list;
    }

    public async Task<DocumentMetadata?> GetByIdAsync(string documentId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var conn = await _dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand($"""
            SELECT document_id, source_name, source_name_normalized, chunk_count, indexed_at, status, version, previous_version_id, is_deprecated, content_hash
            FROM {TableName}
            WHERE document_id = $1
            LIMIT 1
            """, conn);
        cmd.Parameters.AddWithValue(documentId);

        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            return MapRow(reader);
        }

        return null;
    }

    public async Task<DocumentMetadata?> GetLatestBySourceNameAsync(string sourceName)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var normalizedSourceName = NormalizeSourceName(sourceName);

        await using var conn = await _dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand($"""
            SELECT document_id, source_name, source_name_normalized, chunk_count, indexed_at, status, version, previous_version_id, is_deprecated, content_hash
            FROM {TableName}
            WHERE source_name_normalized = $1 AND is_deprecated = FALSE
            ORDER BY version DESC
            LIMIT 1
            """, conn);
        cmd.Parameters.AddWithValue(normalizedSourceName);

        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            return MapRow(reader);
        }

        return null;
    }

    public async Task DeprecateVersionAsync(string documentId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var conn = await _dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            $"UPDATE {TableName} SET is_deprecated = TRUE WHERE document_id = $1",
            conn);
        cmd.Parameters.AddWithValue(documentId);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await InitLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialized) return;

            await using var conn = await _dataSource.OpenConnectionAsync().ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand($"""
                CREATE TABLE IF NOT EXISTS {TableName} (
                    document_id TEXT PRIMARY KEY,
                    source_name TEXT NOT NULL,
                    source_name_normalized TEXT NOT NULL,
                    chunk_count INTEGER NOT NULL,
                    indexed_at TIMESTAMPTZ NOT NULL,
                    status TEXT NOT NULL,
                    version INTEGER NOT NULL,
                    previous_version_id TEXT NULL,
                    is_deprecated BOOLEAN NOT NULL DEFAULT FALSE,
                    content_hash TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_{TableName}_source_active
                    ON {TableName} (source_name_normalized, is_deprecated, version DESC);
                """, conn);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            _initialized = true;
            _logger.LogInformation("Postgres metadata store initialized: {TableName}", TableName);
        }
        finally
        {
            InitLock.Release();
        }
    }

    private static DocumentMetadata MapRow(NpgsqlDataReader reader)
    {
        return new DocumentMetadata
        {
            DocumentId = reader.GetString(0),
            SourceName = reader.GetString(1),
            SourceNameNormalized = reader.GetString(2),
            ChunkCount = reader.GetInt32(3),
            IndexedAt = reader.GetFieldValue<DateTimeOffset>(4),
            Status = Enum.TryParse<IngestionStatus>(reader.GetString(5), out var status)
                ? status
                : IngestionStatus.Failed,
            Version = reader.GetInt32(6),
            PreviousVersionId = reader.IsDBNull(7) ? null : reader.GetString(7),
            IsDeprecated = reader.GetBoolean(8),
            ContentHash = reader.IsDBNull(9) ? null : reader.GetString(9)
        };
    }

    private static string NormalizeSourceName(string sourceName)
    {
        return sourceName.Trim().ToLowerInvariant();
    }

    private static string GetConnectionString(PgVectorOptions? options, IConfiguration configuration)
    {
        // Reuse Aspire connection if present.
        var aspireConnectionString = configuration["ConnectionStrings:pgvector"];
        if (!string.IsNullOrWhiteSpace(aspireConnectionString))
            return aspireConnectionString;

        if (options != null && !string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            var pwd = configuration["PgVector:Password"] ?? Environment.GetEnvironmentVariable("PgVector__Password");
            if (!string.IsNullOrWhiteSpace(pwd))
            {
                var cs = new NpgsqlConnectionStringBuilder(options.ConnectionString) { Password = pwd };
                return cs.ConnectionString;
            }

            return options.ConnectionString;
        }

        var password = configuration["PgVector:Password"] ?? Environment.GetEnvironmentVariable("PgVector__Password");
        if (string.IsNullOrWhiteSpace(password) || options == null)
            return string.Empty;

        return new NpgsqlConnectionStringBuilder
        {
            Host = options.Host,
            Port = options.Port,
            Database = options.Database,
            Username = options.User,
            Password = password
        }.ConnectionString;
    }
}
