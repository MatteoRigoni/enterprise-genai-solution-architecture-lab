using System.Security.Cryptography;
using System.Text.Json;

namespace AiSa.Application.ToolCalling;

/// <summary>
/// Deterministic SHA-256 over canonical JSON of tool arguments (sorted keys, no raw logging).
/// </summary>
public static class ToolProposalArgsHasher
{
    public static string ComputeSha256Hex(ToolCallProposal proposal)
    {
        if (proposal == null)
            throw new ArgumentNullException(nameof(proposal));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            foreach (var key in proposal.Arguments.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                writer.WritePropertyName(key);
                proposal.Arguments[key].WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        var hash = SHA256.HashData(stream.ToArray());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
