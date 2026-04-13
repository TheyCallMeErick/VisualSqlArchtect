using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
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
            provider = metadata.Provider.ToString(),
            databaseName = metadata.DatabaseName,
            schemas = metadata.Schemas
                .OrderBy(static schema => schema.Name, StringComparer.OrdinalIgnoreCase)
                .Select(schema => new
                {
                    schema = schema.Name,
                    tables = schema.Tables
                        .OrderBy(static table => table.Schema, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(static table => table.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(table => new
                        {
                            schema = table.Schema,
                            table = table.Name,
                            kind = table.Kind.ToString(),
                            columns = table.Columns
                                .OrderBy(static column => column.OrdinalPosition)
                                .ThenBy(static column => column.Name, StringComparer.OrdinalIgnoreCase)
                                .Select(column => new
                                {
                                    name = column.Name,
                                    rawType = column.NativeType,
                                    ordinal = column.OrdinalPosition,
                                }),
                            indexes = table.Indexes
                                .OrderBy(static index => index.Name, StringComparer.OrdinalIgnoreCase)
                                .Select(index => new
                                {
                                    name = index.Name,
                                    isUnique = index.IsUnique,
                                    isPrimaryKey = index.IsPrimaryKey,
                                    columns = index.Columns
                                        .OrderBy(static column => column, StringComparer.OrdinalIgnoreCase),
                                }),
                        }),
                }),
            foreignKeys = metadata.AllForeignKeys
                .OrderBy(static foreignKey => foreignKey.ConstraintName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static foreignKey => foreignKey.OrdinalPosition)
                .Select(foreignKey => new
                {
                    name = foreignKey.ConstraintName,
                    childSchema = foreignKey.ChildSchema,
                    childTable = foreignKey.ChildTable,
                    childColumn = foreignKey.ChildColumn,
                    parentSchema = foreignKey.ParentSchema,
                    parentTable = foreignKey.ParentTable,
                    parentColumn = foreignKey.ParentColumn,
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
}
