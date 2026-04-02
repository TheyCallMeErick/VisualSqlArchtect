using System.Text.Json;
using VisualSqlArchitect.UI.Services.Theming;

namespace VisualSqlArchitect.UI.Services.Settings;

public sealed class ThemeJsonOperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int AppliedTokenCount { get; init; }
    public List<string> Warnings { get; init; } = [];
}

public sealed class ThemeJsonSettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private readonly Func<string> _themePathProvider;

    public ThemeJsonSettingsService(Func<string>? themePathProvider = null)
    {
        _themePathProvider = themePathProvider ?? ThemeLoader.GetDefaultThemePath;
    }

    public string GetEditorJsonOrTemplate()
    {
        string path = _themePathProvider();
        if (File.Exists(path))
            return File.ReadAllText(path);

        return "{\n  \"meta\": { \"name\": \"Custom Theme\" },\n  \"colors\": {\n    \"macroBg0\": \"#0B1020\",\n    \"textPrimary\": \"#E8EAED\",\n    \"textSecondary\": \"#8B95A8\"\n  }\n}";
    }

    public ThemeJsonOperationResult ApplyAndPersist(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new ThemeJsonOperationResult
            {
                Success = false,
                Message = "Cole um JSON de tema antes de aplicar.",
            };
        }

        ThemeConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<ThemeConfig>(rawJson, JsonOpts);
        }
        catch (JsonException ex)
        {
            return new ThemeJsonOperationResult
            {
                Success = false,
                Message = $"JSON invalido: {ex.Message}",
            };
        }

        if (config is null)
        {
            return new ThemeJsonOperationResult
            {
                Success = false,
                Message = "JSON invalido: payload vazio.",
            };
        }

        ThemeValidationResult validation = ThemeValidator.Validate(config);
        if (!validation.IsValid)
        {
            return new ThemeJsonOperationResult
            {
                Success = false,
                Message = $"Tema invalido: {string.Join(" | ", validation.Errors)}",
            };
        }

        ThemeTokenMapResult mapped = ThemeTokenMapper.Map(config);
        int applied = ThemeRuntimeApplier.ApplyToCurrentApplication(mapped.TokenOverrides);

        try
        {
            string path = _themePathProvider();
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, rawJson);
        }
        catch (Exception ex)
        {
            return new ThemeJsonOperationResult
            {
                Success = false,
                Message = $"Tema aplicado, mas falhou ao salvar: {ex.Message}",
                AppliedTokenCount = applied,
                Warnings = mapped.Warnings,
            };
        }

        return new ThemeJsonOperationResult
        {
            Success = true,
            Message = "Tema JSON aplicado e salvo.",
            AppliedTokenCount = applied,
            Warnings = mapped.Warnings,
        };
    }

    public ThemeJsonOperationResult RestoreDefault()
    {
        try
        {
            string path = _themePathProvider();
            if (File.Exists(path))
                File.Delete(path);

            return new ThemeJsonOperationResult
            {
                Success = true,
                Message = "Tema personalizado removido. Reinicie o app para voltar totalmente ao tema padrao.",
            };
        }
        catch (Exception ex)
        {
            return new ThemeJsonOperationResult
            {
                Success = false,
                Message = $"Falha ao restaurar tema padrao: {ex.Message}",
            };
        }
    }
}
