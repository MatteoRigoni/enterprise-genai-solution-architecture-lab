namespace AiSa.Infrastructure;

/// <summary>
/// Configuration for PgVector vector store. Password must be supplied via environment variable (no secrets in config).
/// </summary>
public class PgVectorOptions
{
    /// <summary>
    /// PostgreSQL connection string. Prefer building from Host/Port/Database/User and password from env (e.g. PgVector__Password).
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Server host. Used when ConnectionString is not set.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Server port. Used when ConnectionString is not set.
    /// </summary>
    public int Port { get; set; } = 5432;

    /// <summary>
    /// Database name. Used when ConnectionString is not set.
    /// </summary>
    public string Database { get; set; } = "aisa";

    /// <summary>
    /// User name. Used when ConnectionString is not set. Password must come from environment variable.
    /// </summary>
    public string User { get; set; } = "postgres";

    /// <summary>
    /// Timeout in seconds for search operations. Default: 10.
    /// </summary>
    public int SearchTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Timeout in seconds for index operations. Default: 30.
    /// </summary>
    public int IndexTimeoutSeconds { get; set; } = 30;
}
