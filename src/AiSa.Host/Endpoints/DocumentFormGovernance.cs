using AiSa.Application.Models;

namespace AiSa.Host.Endpoints;

/// <summary>
/// Parses governance multipart fields (metadata only; never logs body).
/// </summary>
public static class DocumentFormGovernance
{
    public static bool TryParse(IFormCollection form, int documentVersion, out IngestionGovernanceContext? context, out string? error)
    {
        context = null;
        error = null;

        var classificationRaw = form["classification"].ToString().Trim();
        if (string.IsNullOrEmpty(classificationRaw))
        {
            error = "Field 'classification' is required (Public, Internal, Confidential, Restricted).";
            return false;
        }

        if (!Enum.TryParse<DataClassification>(classificationRaw, ignoreCase: true, out var classification))
        {
            error = "Invalid 'classification'. Use Public, Internal, Confidential, or Restricted.";
            return false;
        }

        if (classification == DataClassification.Restricted)
        {
            error = "Classification 'Restricted' cannot be ingested into the knowledge base.";
            return false;
        }

        var owner = form["owner"].ToString().Trim();
        if (string.IsNullOrEmpty(owner))
        {
            error = "Field 'owner' is required (team or responsible party).";
            return false;
        }

        if (owner.Length > 256)
        {
            error = "Field 'owner' exceeds maximum length.";
            return false;
        }

        var sourceType = form["sourceType"].ToString().Trim();
        if (string.IsNullOrEmpty(sourceType))
            sourceType = "file";

        var confidentialApproved = ParseBool(form["confidentialApproved"].ToString());
        var approvedBy = form["approvedBy"].ToString().Trim();
        if (string.IsNullOrEmpty(approvedBy))
            approvedBy = null;

        if (classification == DataClassification.Confidential)
        {
            if (!confidentialApproved)
            {
                error = "Confidential documents require confidentialApproved=true and an approver.";
                return false;
            }

            if (string.IsNullOrEmpty(approvedBy))
            {
                error = "Field 'approvedBy' is required when classification is Confidential.";
                return false;
            }
        }

        DateTimeOffset? lastReviewedAt = null;
        var lastReviewedRaw = form["lastReviewedAt"].ToString().Trim();
        if (!string.IsNullOrEmpty(lastReviewedRaw))
        {
            if (!DateTimeOffset.TryParse(lastReviewedRaw, out var lastReviewedParsed))
            {
                error = "Invalid 'lastReviewedAt' (use ISO-8601).";
                return false;
            }

            lastReviewedAt = lastReviewedParsed;
        }

        DateTimeOffset? expiresAt = null;
        var expiresRaw = form["expiresAt"].ToString().Trim();
        if (!string.IsNullOrEmpty(expiresRaw))
        {
            if (!DateTimeOffset.TryParse(expiresRaw, out var expiresParsed))
            {
                error = "Invalid 'expiresAt' (use ISO-8601).";
                return false;
            }

            expiresAt = expiresParsed;
        }

        DateTimeOffset? approvedAt = null;
        if (classification == DataClassification.Confidential && confidentialApproved)
            approvedAt = DateTimeOffset.UtcNow;

        context = new IngestionGovernanceContext
        {
            Classification = classification,
            Owner = owner,
            SourceType = sourceType,
            DocumentVersion = documentVersion,
            ConfidentialApproved = confidentialApproved,
            ApprovedBy = approvedBy,
            ApprovedAt = approvedAt,
            LastReviewedAt = lastReviewedAt,
            ExpiresAt = expiresAt
        };

        return true;
    }

    private static bool ParseBool(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return false;
        return raw.Equals("true", StringComparison.OrdinalIgnoreCase)
               || raw.Equals("on", StringComparison.OrdinalIgnoreCase)
               || raw == "1";
    }
}
