using Avalonia.Media;
using VisualSqlArchitect.UI.Services.Localization;

namespace VisualSqlArchitect.UI.Services.Theming;

public sealed class ThemeValidationResult
{
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public bool IsValid => Errors.Count == 0;
}

public static class ThemeValidator
{
    public static ThemeValidationResult Validate(ThemeConfig? config)
    {
        var result = new ThemeValidationResult();

        if (config is null)
        {
            result.Errors.Add(L("themeValidator.error.configNull", "Theme config is null."));
            return result;
        }

        if (config.Colors is null && config.Typography is null)
            result.Warnings.Add(
                L("themeValidator.warning.noSections", "Theme has no colors or typography sections; nothing to apply.")
            );

        if (config.Colors is not null)
        {
            ValidateColor(config.Colors.MacroBg0, "colors.macroBg0", result);
            ValidateColor(config.Colors.MacroBg1, "colors.macroBg1", result);
            ValidateColor(config.Colors.MacroBg2, "colors.macroBg2", result);
            ValidateColor(config.Colors.TextPrimary, "colors.textPrimary", result);
            ValidateColor(config.Colors.TextSecondary, "colors.textSecondary", result);
            ValidateColor(config.Colors.BtnPrimaryBg, "colors.btnPrimaryBg", result);
            ValidateColor(config.Colors.BtnPrimaryFg, "colors.btnPrimaryFg", result);
            ValidateColor(config.Colors.BtnWarningBg, "colors.btnWarningBg", result);
            ValidateColor(config.Colors.BtnWarningFg, "colors.btnWarningFg", result);
        }

        if (config.Typography is not null)
        {
            ValidateSize(config.Typography.TitleSize, "typography.titleSize", result);
            ValidateSize(config.Typography.BodySize, "typography.bodySize", result);
            ValidateSize(config.Typography.MetaSize, "typography.metaSize", result);
        }

        return result;
    }

    private static void ValidateColor(string? value, string key, ThemeValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!Color.TryParse(value, out _))
            result.Warnings.Add(
                string.Format(
                    L("themeValidator.warning.invalidColor", "{0} has invalid color '{1}'. This key will be ignored."),
                    key,
                    value
                )
            );
    }

    private static void ValidateSize(double? value, string key, ThemeValidationResult result)
    {
        if (value is null)
            return;

        if (value < 8 || value > 48)
            result.Warnings.Add(
                string.Format(
                    L("themeValidator.warning.sizeOutOfRange", "{0}={1} is out of range (8..48). This key will be ignored."),
                    key,
                    value
                )
            );
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
