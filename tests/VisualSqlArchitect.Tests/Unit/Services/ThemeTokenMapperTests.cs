using Avalonia.Media;
using VisualSqlArchitect.UI.Services.Theming;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Services;

public class ThemeTokenMapperTests
{
    [Fact]
    public void Map_ValidConfig_ProducesColorBrushAndTypographyOverrides()
    {
        var cfg = new ThemeConfig
        {
            Colors = new ThemeColorsConfig
            {
                MacroBg0 = "#010203",
                BtnPrimaryBg = "#112233"
            },
            Typography = new ThemeTypographyConfig
            {
                UiFont = "Segoe UI,Arial",
                TitleSize = 14
            }
        };

        ThemeTokenMapResult mapped = ThemeTokenMapper.Map(cfg);

        Assert.True(mapped.TokenOverrides.ContainsKey("MacroBg0"));
        Assert.True(mapped.TokenOverrides.ContainsKey("MacroBg0Brush"));
        Assert.IsType<SolidColorBrush>(mapped.TokenOverrides["MacroBg0Brush"]);
        Assert.True(mapped.TokenOverrides.ContainsKey("BtnPrimaryBg"));
        Assert.True(mapped.TokenOverrides.ContainsKey("BtnPrimaryBgBrush"));
        Assert.True(mapped.TokenOverrides.ContainsKey("UIFont"));
        Assert.True(mapped.TokenOverrides.ContainsKey("FontSizeTitle"));
    }

    [Fact]
    public void Map_InvalidEntries_AreIgnoredWithWarnings()
    {
        var cfg = new ThemeConfig
        {
            Colors = new ThemeColorsConfig { MacroBg1 = "invalid" },
            Typography = new ThemeTypographyConfig { BodySize = 100 }
        };

        ThemeTokenMapResult mapped = ThemeTokenMapper.Map(cfg);

        Assert.False(mapped.TokenOverrides.ContainsKey("MacroBg1"));
        Assert.False(mapped.TokenOverrides.ContainsKey("FontSizeBody"));
        Assert.NotEmpty(mapped.Warnings);
    }
}
