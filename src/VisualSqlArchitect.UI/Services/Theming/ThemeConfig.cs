namespace VisualSqlArchitect.UI.Services.Theming;

public sealed class ThemeConfig
{
    public ThemeMetaConfig? Meta { get; set; }
    public ThemeColorsConfig? Colors { get; set; }
    public ThemeTypographyConfig? Typography { get; set; }
}

public sealed class ThemeMetaConfig
{
    public string? Name { get; set; }
    public string? Version { get; set; }
}

public sealed class ThemeColorsConfig
{
    public string? MacroBg0 { get; set; }
    public string? MacroBg1 { get; set; }
    public string? MacroBg2 { get; set; }
    public string? TextPrimary { get; set; }
    public string? TextSecondary { get; set; }
    public string? BtnPrimaryBg { get; set; }
    public string? BtnPrimaryFg { get; set; }
    public string? BtnWarningBg { get; set; }
    public string? BtnWarningFg { get; set; }
}

public sealed class ThemeTypographyConfig
{
    public string? UiFont { get; set; }
    public string? MonoFont { get; set; }
    public double? TitleSize { get; set; }
    public double? BodySize { get; set; }
    public double? MetaSize { get; set; }
}
