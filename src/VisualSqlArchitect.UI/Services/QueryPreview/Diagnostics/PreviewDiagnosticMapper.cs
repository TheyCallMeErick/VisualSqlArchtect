
namespace VisualSqlArchitect.UI.Services.QueryPreview;

public static class PreviewDiagnosticMapper
{
    private sealed record Rule(
        Regex Pattern,
        EPreviewDiagnosticCategory Category,
        EPreviewDiagnosticSeverity Severity,
        string Code
    );

    private static readonly IReadOnlyList<Rule> Rules =
    [
        new(
            new Regex("cycle detected|recursive", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            EPreviewDiagnosticCategory.Cte,
            EPreviewDiagnosticSeverity.Error,
            "E-CTE-001"
        ),
        new(
            new Regex("\\bcte\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            EPreviewDiagnosticCategory.Cte,
            EPreviewDiagnosticSeverity.Warning,
            "W-CTE-001"
        ),
        new(
            new Regex("\\bwindow\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            EPreviewDiagnosticCategory.Window,
            EPreviewDiagnosticSeverity.Warning,
            "W-WIN-001"
        ),
        new(
            new Regex("\\bjoin\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            EPreviewDiagnosticCategory.Join,
            EPreviewDiagnosticSeverity.Warning,
            "W-JOIN-001"
        ),
        new(
            new Regex("\\bsubquery\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            EPreviewDiagnosticCategory.Subquery,
            EPreviewDiagnosticSeverity.Warning,
            "W-SUB-001"
        ),
        new(
            new Regex("type mismatch|incompatible|pin type", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            EPreviewDiagnosticCategory.TypeCompatibility,
            EPreviewDiagnosticSeverity.Warning,
            "W-TYP-001"
        ),
        new(
            new Regex("comparison|between|like", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            EPreviewDiagnosticCategory.Comparison,
            EPreviewDiagnosticSeverity.Warning,
            "W-CMP-001"
        ),
        new(
            new Regex("where|having|predicate|logic", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            EPreviewDiagnosticCategory.Predicate,
            EPreviewDiagnosticSeverity.Warning,
            "W-PRD-001"
        ),
    ];

    public static List<PreviewDiagnostic> FromLegacyMessages(IEnumerable<string> messages)
    {
        return messages
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(FromLegacyMessage)
            .ToList();
    }

    public static PreviewDiagnostic FromLegacyMessage(string message)
    {
        foreach (Rule rule in Rules)
        {
            if (!rule.Pattern.IsMatch(message))
                continue;

            EPreviewDiagnosticSeverity severity = UpgradeSeverityIfNeeded(message, rule.Severity);
            string code = severity == EPreviewDiagnosticSeverity.Error && rule.Code.StartsWith("W-", StringComparison.Ordinal)
                ? "E-" + rule.Code[2..]
                : rule.Code;

            return new PreviewDiagnostic(severity, rule.Category, code, message);
        }

        EPreviewDiagnosticSeverity fallbackSeverity = InferFallbackSeverity(message);
        string fallbackCode = fallbackSeverity == EPreviewDiagnosticSeverity.Error ? "E-GEN-001" : "W-GEN-001";
        return new PreviewDiagnostic(fallbackSeverity, EPreviewDiagnosticCategory.General, fallbackCode, message);
    }

    private static EPreviewDiagnosticSeverity UpgradeSeverityIfNeeded(string message, EPreviewDiagnosticSeverity current)
    {
        EPreviewDiagnosticSeverity inferred = InferFallbackSeverity(message);
        return inferred > current ? inferred : current;
    }

    private static EPreviewDiagnosticSeverity InferFallbackSeverity(string message)
    {
        if (message.StartsWith("info:", StringComparison.OrdinalIgnoreCase))
            return EPreviewDiagnosticSeverity.Info;

        if (message.Contains("missing required", StringComparison.OrdinalIgnoreCase)
            || message.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cycle detected", StringComparison.OrdinalIgnoreCase)
            || message.Contains("requires", StringComparison.OrdinalIgnoreCase)
            || message.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            return EPreviewDiagnosticSeverity.Error;
        }

        return EPreviewDiagnosticSeverity.Warning;
    }
}


