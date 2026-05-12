namespace AiSa.Application.Models;

/// <summary>
/// Document data classification for RAG ingestion (see docs/governance.md).
/// </summary>
public enum DataClassification
{
    Public = 0,
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}
