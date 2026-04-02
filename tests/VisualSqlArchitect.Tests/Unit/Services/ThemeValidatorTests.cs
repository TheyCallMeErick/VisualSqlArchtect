using VisualSqlArchitect.UI.Services.Theming;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Services;

public class ThemeValidatorTests
{
    [Fact]
    public void Validate_NullConfig_ReturnsError()
    {
        ThemeValidationResult result = ThemeValidator.Validate(null);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("null", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_InvalidColor_AddsWarningButRemainsValid()
    {
        var cfg = new ThemeConfig
        {
            Colors = new ThemeColorsConfig
            {
                MacroBg0 = "not-a-color"
            }
        };

        ThemeValidationResult result = ThemeValidator.Validate(cfg);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("macroBg0", StringComparison.OrdinalIgnoreCase));
    }
}
