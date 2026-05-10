using AiSa.Application;
using AiSa.Application.Models;

namespace AiSa.Infrastructure;

/// <summary>
/// Deterministic retrieval stub for CI eval smoke (AISA_CI_EVAL=1). Returns FAQ-shaped chunks without embeddings, Azure Search, or Postgres.
/// </summary>
public sealed class CiEvalFaqStubRetrievalService : IRetrievalService
{
    private const string FaqBody =
        """
        AcmeCloud is a cloud storage service. Users can upload, organize, and share files securely across devices.
        You can reset your password on AcmeCloud from Settings, Security, Reset Password, and you will receive an email with a reset link.
        The password reset link is valid for 15 minutes.
        Supported file types include PDF, DOCX, JPG, PNG, MP4, and ZIP.
        Executable files are blocked on AcmeCloud and are not allowed.
        The free plan includes 5 GB of storage. You get 5 GB for free on AcmeCloud.
        You can share files with people outside your organization using a share link with external users.
        The following sharing permissions are available: View only, Comment, and Edit. You can share a file using a share link and permissions.
        Deleted files are kept in Trash for 30 days before they are permanently removed. When you delete files they go to Trash for 30 days.
        You can recover a deleted file from Trash within 30 days.
        Monthly subscriptions are non-refundable on AcmeCloud.
        Yearly subscriptions can receive a refund within 14 days if usage is under 1 GB.
        Contact AcmeCloud support through in-app chat or email.
        Support hours are Monday through Friday from 9:00 to 18:00 UTC. Weekend support is not available outside Monday through Friday.
        """;

    public Task<IEnumerable<SearchResult>> RetrieveAsync(
        string query,
        int topK,
        CancellationToken cancellationToken = default)
    {
        _ = query;
        _ = topK;

        var chunk = new DocumentChunk
        {
            ChunkId = "chunk-ci-faq",
            SourceId = "doc-faq",
            SourceName = "faq.txt",
            Content = FaqBody,
            ChunkIndex = 0,
            Vector = new float[1536],
            IndexedAt = DateTimeOffset.UtcNow
        };

        var results = new List<SearchResult>
        {
            new()
            {
                Chunk = chunk,
                Score = 0.99f
            }
        };

        return Task.FromResult<IEnumerable<SearchResult>>(results);
    }
}
