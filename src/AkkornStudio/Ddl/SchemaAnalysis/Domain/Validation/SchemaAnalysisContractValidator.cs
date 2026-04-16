using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;

namespace AkkornStudio.Ddl.SchemaAnalysis.Domain.Validation;

public sealed class SchemaAnalysisContractValidator
{
    public IReadOnlyList<SchemaContractValidationError> Validate(
        SchemaAnalysisResult result,
        SchemaAnalysisProfile profile
    )
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(profile);

        List<SchemaContractValidationError> errors = [];

        if (result.Issues.Count > profile.MaxIssues)
        {
            errors.Add(
                new SchemaContractValidationError(
                    "ANL-VAL-MAX-ISSUES",
                    $"Issues.Count={result.Issues.Count} excede MaxIssues={profile.MaxIssues}.",
                    "result.issues"
                )
            );
        }

        for (int issueIndex = 0; issueIndex < result.Issues.Count; issueIndex++)
        {
            ValidateIssue(result.Issues[issueIndex], issueIndex, profile, errors);
        }

        return errors;
    }

    public void EnsureValid(SchemaAnalysisResult result, SchemaAnalysisProfile profile)
    {
        IReadOnlyList<SchemaContractValidationError> errors = Validate(result, profile);
        if (errors.Count == 0)
        {
            return;
        }

        string message = string.Join(
            Environment.NewLine,
            errors.Select(e => $"[{e.Code}] {e.Path}: {e.Message}")
        );

        throw new ArgumentException(message, nameof(result));
    }

    private static void ValidateIssue(
        SchemaIssue issue,
        int issueIndex,
        SchemaAnalysisProfile profile,
        List<SchemaContractValidationError> errors
    )
    {
        if (issue.Evidence.Count < 1)
        {
            errors.Add(
                new SchemaContractValidationError(
                    "ANL-VAL-EVIDENCE-EMPTY",
                    "Issue deve conter ao menos uma evidência.",
                    $"result.issues[{issueIndex}].evidence"
                )
            );
        }

        if (issue.Suggestions.Count > profile.MaxSuggestionsPerIssue)
        {
            errors.Add(
                new SchemaContractValidationError(
                    "ANL-VAL-MAX-SUGGESTIONS",
                    $"Suggestions.Count={issue.Suggestions.Count} excede MaxSuggestionsPerIssue={profile.MaxSuggestionsPerIssue}.",
                    $"result.issues[{issueIndex}].suggestions"
                )
            );
        }

        if (!IsConfidenceRounded(issue.Confidence))
        {
            errors.Add(
                new SchemaContractValidationError(
                    "ANL-VAL-CONFIDENCE-ROUNDING",
                    "Confidence deve estar arredondada com Math.Round(v, 4, MidpointRounding.ToEven).",
                    $"result.issues[{issueIndex}].confidence"
                )
            );
        }

        if (issue.TargetType == SchemaTargetType.Column)
        {
            if (string.IsNullOrWhiteSpace(issue.TableName) || string.IsNullOrWhiteSpace(issue.ColumnName))
            {
                errors.Add(
                    new SchemaContractValidationError(
                        "ANL-VAL-TARGET-COLUMN",
                        "TargetType=Column exige TableName e ColumnName não vazios.",
                        $"result.issues[{issueIndex}]"
                    )
                );
            }
        }

        if (issue.TargetType == SchemaTargetType.Constraint)
        {
            if (string.IsNullOrWhiteSpace(issue.ConstraintName))
            {
                errors.Add(
                    new SchemaContractValidationError(
                        "ANL-VAL-TARGET-CONSTRAINT",
                        "TargetType=Constraint exige ConstraintName não vazio.",
                        $"result.issues[{issueIndex}].constraintName"
                    )
                );
            }
        }

        for (int suggestionIndex = 0; suggestionIndex < issue.Suggestions.Count; suggestionIndex++)
        {
            SchemaSuggestion suggestion = issue.Suggestions[suggestionIndex];
            if (!IsConfidenceRounded(suggestion.Confidence))
            {
                errors.Add(
                    new SchemaContractValidationError(
                        "ANL-VAL-CONFIDENCE-ROUNDING",
                        "Confidence de suggestion deve estar arredondada com Math.Round(v, 4, MidpointRounding.ToEven).",
                        $"result.issues[{issueIndex}].suggestions[{suggestionIndex}].confidence"
                    )
                );
            }

            for (
                int candidateIndex = 0;
                candidateIndex < suggestion.SqlCandidates.Count;
                candidateIndex++
            )
            {
                ValidateCandidate(
                    suggestion.SqlCandidates[candidateIndex],
                    issueIndex,
                    suggestionIndex,
                    candidateIndex,
                    errors
                );
            }
        }
    }

    private static void ValidateCandidate(
        SqlFixCandidate candidate,
        int issueIndex,
        int suggestionIndex,
        int candidateIndex,
        List<SchemaContractValidationError> errors
    )
    {
        string pathBase =
            $"result.issues[{issueIndex}].suggestions[{suggestionIndex}].sqlCandidates[{candidateIndex}]";

        if (candidate.IsAutoApplicable && candidate.Safety != SqlCandidateSafety.NonDestructive)
        {
            errors.Add(
                new SchemaContractValidationError(
                    "ANL-VAL-AUTO-APPLICABLE-SAFETY",
                    "IsAutoApplicable=true somente é permitido quando Safety=NonDestructive.",
                    $"{pathBase}.isAutoApplicable"
                )
            );
        }

        if (
            candidate.Visibility == CandidateVisibility.VisibleActionable
            && candidate.Safety != SqlCandidateSafety.NonDestructive
        )
        {
            errors.Add(
                new SchemaContractValidationError(
                    "ANL-VAL-VISIBILITY-SAFETY",
                    "CandidateVisibility=VisibleActionable somente é permitido quando Safety=NonDestructive.",
                    $"{pathBase}.visibility"
                )
            );
        }

        if (candidate.PreconditionsSql.Count < 1)
        {
            errors.Add(
                new SchemaContractValidationError(
                    "ANL-VAL-PRECONDITIONS-REQUIRED",
                    "PreconditionsSql é obrigatório e não pode ser vazio no MVP.",
                    $"{pathBase}.preconditionsSql"
                )
            );
        }
    }

    private static bool IsConfidenceRounded(double value)
    {
        double rounded = Math.Round(value, 4, MidpointRounding.ToEven);
        return Math.Abs(value - rounded) < 0.0000000001;
    }
}
