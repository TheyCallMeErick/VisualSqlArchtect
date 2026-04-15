using System.Security.Cryptography;
using System.Text;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Processing;

public static class SchemaIssueTextNormalizer
{
    public static string NormalizeForHash(string? text)
    {
        if (text is null)
        {
            return "∅";
        }

        string trimmed = text.Normalize(NormalizationForm.FormC).Trim();
        string collapsed = string.Join(" ", trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.ToLowerInvariant();
    }

    public static string ComputeSha256Hex(string payload)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
