using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Normalization;
using AkkornStudio.Core;
using AkkornStudio.Metadata;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Validation;

public sealed class SchemaMetadataValidator
{
    public SchemaMetadataValidationResult Validate(DbMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        List<SchemaRuleExecutionDiagnostic> diagnostics = [];

        if (string.IsNullOrWhiteSpace(metadata.DatabaseName))
        {
            diagnostics.Add(CreateFatalDiagnostic("DatabaseName ausente no snapshot."));
        }

        foreach (TableMetadata table in metadata.AllTables)
        {
            ValidateTable(metadata.Provider, table, diagnostics);
        }

        foreach (ForeignKeyRelation foreignKey in metadata.AllForeignKeys)
        {
            ValidateForeignKey(foreignKey, diagnostics);
        }

        if (diagnostics.Any(static diagnostic => diagnostic.IsFatal))
        {
            return new SchemaMetadataValidationResult(false, OrderDiagnostics(diagnostics));
        }

        AddPartialDiagnostics(metadata, diagnostics);

        return new SchemaMetadataValidationResult(true, OrderDiagnostics(diagnostics));
    }

    private static void ValidateTable(
        DatabaseProvider provider,
        TableMetadata table,
        ICollection<SchemaRuleExecutionDiagnostic> diagnostics
    )
    {
        if (string.IsNullOrWhiteSpace(table.Name))
        {
            diagnostics.Add(CreateFatalDiagnostic("Tabela sem nome qualificado mínimo."));
        }

        if (provider != DatabaseProvider.MySql)
        {
            string? canonicalSchema = SchemaCanonicalizer.Normalize(provider, table.Schema);
            if (string.IsNullOrWhiteSpace(canonicalSchema))
            {
                diagnostics.Add(CreateFatalDiagnostic($"Schema inválido para a tabela '{table.Name}'."));
            }
        }

        foreach (ColumnMetadata column in table.Columns)
        {
            if (string.IsNullOrWhiteSpace(column.Name))
            {
                diagnostics.Add(
                    CreateFatalDiagnostic(
                        $"Coluna inválida sem nome em '{table.FullName}'."
                    )
                );
            }

            if (string.IsNullOrWhiteSpace(column.NativeType))
            {
                diagnostics.Add(
                    CreateFatalDiagnostic(
                        $"Coluna '{table.FullName}.{column.Name}' sem rawType no snapshot."
                    )
                );
            }

            if (column.OrdinalPosition <= 0)
            {
                diagnostics.Add(
                    CreateFatalDiagnostic(
                        $"Coluna '{table.FullName}.{column.Name}' sem ordinal válido."
                    )
                );
            }
        }
    }

    private static void ValidateForeignKey(
        ForeignKeyRelation foreignKey,
        ICollection<SchemaRuleExecutionDiagnostic> diagnostics
    )
    {
        if (
            string.IsNullOrWhiteSpace(foreignKey.ConstraintName)
            || string.IsNullOrWhiteSpace(foreignKey.ChildTable)
            || string.IsNullOrWhiteSpace(foreignKey.ChildColumn)
            || string.IsNullOrWhiteSpace(foreignKey.ParentTable)
            || string.IsNullOrWhiteSpace(foreignKey.ParentColumn)
        )
        {
            diagnostics.Add(CreateFatalDiagnostic("FK catalogada com alvo técnico incompleto."));
        }
    }

    private static void AddPartialDiagnostics(
        DbMetadata metadata,
        ICollection<SchemaRuleExecutionDiagnostic> diagnostics
    )
    {
        HashSet<SchemaRuleCode> emittedRules = diagnostics
            .Where(static diagnostic => diagnostic.Code == "ANL-METADATA-PARTIAL")
            .Select(static diagnostic => diagnostic.RuleCode)
            .OfType<SchemaRuleCode>()
            .ToHashSet();

        if (
            metadata.Provider != DatabaseProvider.SQLite
            && metadata.AllTables.Any()
            && metadata.AllTables.All(static table =>
                string.IsNullOrWhiteSpace(table.Comment)
                && table.Columns.All(static column => string.IsNullOrWhiteSpace(column.Comment))
            )
        )
        {
            AddPartialDiagnosticForRule(
                SchemaRuleCode.MISSING_REQUIRED_COMMENT,
                emittedRules,
                diagnostics
            );
        }

        if (metadata.AllTables.Any() && metadata.AllTables.All(static table => table.Indexes.Count == 0))
        {
            AddPartialDiagnosticForRule(SchemaRuleCode.MISSING_FK, emittedRules, diagnostics);
        }

        if (metadata.AllTables.Any(static table => table.EstimatedRowCount is null))
        {
            AddPartialDiagnosticForRule(
                SchemaRuleCode.NF2_HINT_PARTIAL_DEPENDENCY,
                emittedRules,
                diagnostics
            );
        }
    }

    private static void AddPartialDiagnosticForRule(
        SchemaRuleCode ruleCode,
        ISet<SchemaRuleCode> emittedRules,
        ICollection<SchemaRuleExecutionDiagnostic> diagnostics
    )
    {
        if (!emittedRules.Add(ruleCode))
        {
            return;
        }

        diagnostics.Add(
            new SchemaRuleExecutionDiagnostic(
                Code: "ANL-METADATA-PARTIAL",
                Message: "Metadado necessário à regra não está disponível no snapshot.",
                RuleCode: ruleCode,
                State: RuleExecutionState.Skipped,
                IsFatal: false
            )
        );
    }

    private static SchemaRuleExecutionDiagnostic CreateFatalDiagnostic(string message)
    {
        return new SchemaRuleExecutionDiagnostic(
            Code: "ANL-METADATA-INVALID",
            Message: message,
            RuleCode: null,
            State: RuleExecutionState.Failed,
            IsFatal: true
        );
    }

    private static IReadOnlyList<SchemaRuleExecutionDiagnostic> OrderDiagnostics(
        IEnumerable<SchemaRuleExecutionDiagnostic> diagnostics
    )
    {
        return diagnostics
            .OrderByDescending(static diagnostic => diagnostic.IsFatal)
            .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(
                static diagnostic => diagnostic.RuleCode.HasValue ? 0 : 1
            )
            .ThenBy(static diagnostic => diagnostic.RuleCode)
            .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToList();
    }
}
