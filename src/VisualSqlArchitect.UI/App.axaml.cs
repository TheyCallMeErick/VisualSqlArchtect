using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VisualSqlArchitect.UI.Services.Settings;
using VisualSqlArchitect.UI.Services.Theming;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Validation.Conventions;
using VisualSqlArchitect.UI.ViewModels.Validation.Conventions.Implementations;

namespace VisualSqlArchitect.UI;

public partial class App : Application
{
    private static readonly ILogger<App> _logger = NullLogger<App>.Instance;
    private IServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ApplySavedThemeVariant();
        ApplyUserThemeIfPresent();

        _services = BuildServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = _services.GetRequiredService<MainWindow>();

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddVisualSqlArchitect();
        services.AddSingleton<IAliasConvention, SnakeCaseConvention>();
        services.AddSingleton<IAliasConvention, CamelCaseConvention>();
        services.AddSingleton<IAliasConvention, PascalCaseConvention>();
        services.AddSingleton<IAliasConvention, ScreamingSnakeCaseConvention>();
        services.AddSingleton<IAliasConventionRegistry, AliasConventionRegistry>();
        services.AddSingleton<ThemeJsonSettingsService>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }

    private static void ApplySavedThemeVariant()
    {
        if (Application.Current is null)
            return;

        AppSettings settings = AppSettingsStore.Load();
        Application.Current.RequestedThemeVariant =
            settings.ThemeVariant.Equals("Light", StringComparison.OrdinalIgnoreCase)
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
    }

    private static void ApplyUserThemeIfPresent()
    {
        string path = ThemeLoader.GetDefaultThemePath();
        ThemeLoadResult load = ThemeLoader.LoadFromPath(path);
        if (load.Status == ThemeLoadStatus.NotFound)
            return;

        if (load.Status != ThemeLoadStatus.Loaded || load.Config is null)
        {
            _logger.LogWarning("Theme fallback: {Status} - {Message}", load.Status, load.Message);
            return;
        }

        ThemeValidationResult validation = ThemeValidator.Validate(load.Config);
        foreach (string error in validation.Errors)
            _logger.LogError("Theme validation error: {Error}", error);
        foreach (string warning in validation.Warnings)
            _logger.LogWarning("Theme validation warning: {Warning}", warning);

        if (!validation.IsValid)
        {
            _logger.LogWarning("Theme fallback: invalid configuration");
            return;
        }

        ThemeTokenMapResult mapped = ThemeTokenMapper.Map(load.Config);
        foreach (string warning in mapped.Warnings)
            _logger.LogWarning("Theme mapping warning: {Warning}", warning);

        int applied = ThemeRuntimeApplier.ApplyToCurrentApplication(mapped.TokenOverrides);
        _logger.LogInformation("Theme loaded: applied {AppliedCount} token override(s) from {Path}", applied, path);
    }
}

// ── Program entry point ───────────────────────────────────────────────────────

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
