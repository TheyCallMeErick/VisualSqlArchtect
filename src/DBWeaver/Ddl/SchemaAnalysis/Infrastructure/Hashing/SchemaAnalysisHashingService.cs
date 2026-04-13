using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;
using DBWeaver.Ddl.SchemaAnalysis.Infrastructure.Serialization;
using DBWeaver.Metadata;

namespace DBWeaver.Ddl.SchemaAnalysis.Infrastructure.Hashing;

public sealed class SchemaAnalysisHashingService
{
    private readonly SchemaAnalysisCanonicalJsonSerializer _profileSerializer = new();

    public string ComputeProfileContentHash(SchemaAnalysisProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return ComputeSha256Hex(_profileSerializer.SerializeProfileCanonical(profile));
    }

    public string ComputeMetadataFingerprint(DbMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var payload = new
        {
            provider = NormalizeText(metadata.Provider.ToString()),
            databaseName = NormalizeText(metadata.DatabaseName),
            schemas = metadata.Schemas
                .OrderBy(schema => NormalizeText(SchemaCanonicalizer.Normalize(metadata.Provider, schema.Name) ?? schema.Name), StringComparer.Ordinal)
                .Select(schema => new
                {
                    schema = NormalizeText(SchemaCanonicalizer.Normalize(metadata.Provider, schema.Name) ?? schema.Name),
                    tables = schema.Tables
                        .OrderBy(table => NormalizeText(SchemaCanonicalizer.Normalize(metadata.Provider, table.Schema) ?? table.Schema), StringComparer.Ordinal)
                        .ThenBy(table => NormalizeText(table.Name), StringComparer.Ordinal)
                        .Select(table => new
                        {
                            schema = NormalizeText(SchemaCanonicalizer.Normalize(metadata.Provider, table.Schema) ?? table.Schema),
                            table = NormalizeText(table.Name),
                            kind = table.Kind.ToString(),
                            columns = table.Columns
                                .OrderBy(static column => column.OrdinalPosition)
                                .ThenBy(column => NormalizeText(column.Name), StringComparer.Ordinal)
                                .Select(column => new
                                {
                                    name = NormalizeText(column.Name),
                                    rawType = NormalizeText(column.NativeType),
                                    ordinal = column.OrdinalPosition,
                                }),
                            indexes = table.Indexes
                                .OrderBy(index => NormalizeText(index.Name), StringComparer.Ordinal)
                                .Select(index => new
                                {
                                    name = NormalizeText(index.Name),
                                    isUnique = index.IsUnique,
                                    isPrimaryKey = index.IsPrimaryKey,
                                    columns = index.Columns
                                        .Select(NormalizeText)
                                        .OrderBy(static column => column, StringComparer.Ordinal),
                                }),
                        }),
                }),
            foreignKeys = metadata.AllForeignKeys
                .OrderBy(foreignKey => NormalizeText(foreignKey.ConstraintName), StringComparer.Ordinal)
                .ThenBy(static foreignKey => foreignKey.OrdinalPosition)
                .Select(foreignKey => new
                {
                    name = NormalizeText(foreignKey.ConstraintName),
                    childSchema = NormalizeText(SchemaCanonicalizer.Normalize(metadata.Provider, foreignKey.ChildSchema) ?? foreignKey.ChildSchema),
                    childTable = NormalizeText(foreignKey.ChildTable),
                    childColumn = NormalizeText(foreignKey.ChildColumn),
                    parentSchema = NormalizeText(SchemaCanonicalizer.Normalize(metadata.Provider, foreignKey.ParentSchema) ?? foreignKey.ParentSchema),
                    parentTable = NormalizeText(foreignKey.ParentTable),
                    parentColumn = NormalizeText(foreignKey.ParentColumn),
                    ordinal = foreignKey.OrdinalPosition,
                }),
        };

        string json = JsonSerializer.Serialize(payload);
        return ComputeSha256Hex(json);
    }

    public string ComputeCacheKey(
        string metadataFingerprint,
        string profileContentHash,
        string provider,
        int specVersion
    )
    {
        return ComputeSha256Hex($"{metadataFingerprint}|{profileContentHash}|{provider}|{specVersion}");
    }

    private static string ComputeSha256Hex(string payload)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeText(string value)
    {
        return value.Normalize(NormalizationForm.FormC).Trim();
    }
}
