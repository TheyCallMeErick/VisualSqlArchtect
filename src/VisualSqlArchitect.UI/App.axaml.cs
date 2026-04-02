using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using VisualSqlArchitect.UI.Services.Settings;
using VisualSqlArchitect.UI.Services.Theming;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI;

public partial class App : Application
{
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
            Console.WriteLine($"[Theme] fallback: {load.Status} - {load.Message}");
            return;
        }

        ThemeValidationResult validation = ThemeValidator.Validate(load.Config);
        foreach (string error in validation.Errors)
            Console.WriteLine($"[Theme] error: {error}");
        foreach (string warning in validation.Warnings)
            Console.WriteLine($"[Theme] warning: {warning}");

        if (!validation.IsValid)
        {
            Console.WriteLine("[Theme] fallback: invalid configuration.");
            return;
        }

        ThemeTokenMapResult mapped = ThemeTokenMapper.Map(load.Config);
        foreach (string warning in mapped.Warnings)
            Console.WriteLine($"[Theme] warning: {warning}");

        int applied = ThemeRuntimeApplier.ApplyToCurrentApplication(mapped.TokenOverrides);
        Console.WriteLine($"[Theme] loaded: applied {applied} token override(s) from {path}");
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
            .WithInterFont() // Avalonia.Fonts.Inter
            .LogToTrace();
}
