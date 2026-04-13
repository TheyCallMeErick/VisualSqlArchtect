using System.Security.Cryptography;
using System.Text;

namespace DBWeaver.SqlImport.Ids;

public static class StableSqlImportIdGenerator
{
    public const string CurrentIdSchemeVersion = "1.0.0";
    public const int DefaultIdLength = 16;

    private const string Base32Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

    public static IdGenerationMeta CreateDefaultMeta()
    {
        return new IdGenerationMeta(
            CurrentIdSchemeVersion,
            "SHA-256",
            "base32-lower",
            DefaultIdLength
        );
    }

    public static string BuildQueryId(
        string dialect,
        string sourceHash,
        IReadOnlyCollection<string> featureFlags
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dialect);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceHash);

        string flags = featureFlags.Count == 0
            ? "none"
            : string.Join(',', featureFlags.OrderBy(static value => value, StringComparer.Ordinal));

        string payload = $"Q|{dialect}|{sourceHash}|{flags}";
        return BuildId(payload);
    }

    public static string BuildJoinId(
        string queryId,
        int joinOrdinal,
        string joinType,
        string rightSourceKey,
        string onExprAstHash
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(joinType);
        ArgumentException.ThrowIfNullOrWhiteSpace(rightSourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(onExprAstHash);

        string payload = $"J|{queryId}|{joinOrdinal}|{joinType}|{rightSourceKey}|{onExprAstHash}";
        return BuildId(payload);
    }

    public static string BuildSelectItemId(
        string queryId,
        int selectOrdinal,
        string exprAstHash,
        string normalizedAlias
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(exprAstHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedAlias);

        string payload = $"S|{queryId}|{selectOrdinal}|{exprAstHash}|{normalizedAlias}";
        return BuildId(payload);
    }

    public static string BuildExprId(
        string queryId,
        string exprPath,
        string nodeKind,
        string exprAstHash,
        string? parentExprId = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(exprPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(exprAstHash);

        string payload =
            $"E|{queryId}|{exprPath}|{nodeKind}|{exprAstHash}|{parentExprId ?? "root"}";
        return BuildId(payload);
    }

    public static string BuildSourceId(
        string queryId,
        string scopePath,
        int sourceOrdinal,
        string sourceSignature
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSignature);

        string payload = $"R|{queryId}|{scopePath}|{sourceOrdinal}|{sourceSignature}";
        return BuildId(payload);
    }

    public static string BuildScopeId(string queryId, string scopePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopePath);

        string payload = $"SC|{queryId}|{scopePath}";
        return BuildId(payload);
    }

    public static string BuildId(string canonicalPayload, int idLength = DefaultIdLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPayload);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(idLength);

        byte[] payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
        byte[] hash = SHA256.HashData(payloadBytes);
        string base32 = ToBase32Lower(hash);

        return base32.Length <= idLength ? base32 : base32[..idLength];
    }

    private static string ToBase32Lower(IReadOnlyList<byte> bytes)
    {
        var builder = new StringBuilder((bytes.Count * 8 + 4) / 5);

        int buffer = 0;
        int bitsInBuffer = 0;

        for (int index = 0; index < bytes.Count; index++)
        {
            buffer = (buffer << 8) | bytes[index];
            bitsInBuffer += 8;

            while (bitsInBuffer >= 5)
            {
                int base32Index = (buffer >> (bitsInBuffer - 5)) & 0x1F;
                builder.Append(Base32Alphabet[base32Index]);
                bitsInBuffer -= 5;
            }
        }

        if (bitsInBuffer > 0)
        {
            int base32Index = (buffer << (5 - bitsInBuffer)) & 0x1F;
            builder.Append(Base32Alphabet[base32Index]);
        }

        return builder.ToString();
    }
}
