using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AkkornStudio.UI.Services;
using AkkornStudio.UI.Services.Localization;
using AkkornStudio.UI.Services.Theming;

namespace AkkornStudio.UI.Controls.Query;

internal sealed class QueryParameterPromptWindow : Window
{
    private readonly IReadOnlyList<QueryParameterPlaceholder> _placeholders;
    private readonly Dictionary<QueryParameterPlaceholder, TextBox> _inputs = [];
    private readonly IReadOnlyDictionary<string, string> _initialValues;
    private readonly IReadOnlyDictionary<string, QueryParameter> _suggestedParameters;
    private readonly string _sql;

    public QueryParameterPromptWindow(
        string sql,
        IReadOnlyList<QueryParameterPlaceholder> placeholders,
        IReadOnlyDictionary<string, string>? initialValues = null,
        IReadOnlyDictionary<string, QueryParameter>? suggestedParameters = null)
    {
        _sql = sql;
        _placeholders = placeholders;
        _initialValues = initialValues ?? new Dictionary<string, string>();
        _suggestedParameters = suggestedParameters ?? new Dictionary<string, QueryParameter>();

        Title = L("queryParameters.dialog.title", "Preview parameters");
        Width = 640;
        Height = 520;
        MinWidth = 520;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse(UiColorConstants.C_0D0F14));
        KeyDown += OnKeyDown;
        Content = BuildContent();
    }

    public IReadOnlyList<QueryParameter>? Result { get; private set; }

    public IReadOnlyDictionary<string, string> EnteredValues { get; private set; } =
        new Dictionary<string, string>();

    private Control BuildContent()
    {
        StackPanel rows = new()
        {
            Spacing = 10,
        };

        foreach (QueryParameterPlaceholder placeholder in _placeholders)
        {
            string placeholderKey = QueryParameterPlaceholderParser.GetStorageKey(placeholder);
            _suggestedParameters.TryGetValue(placeholderKey, out QueryParameter? suggestedParameter);
            QueryParameterHint hint = QueryParameterHintResolver.Resolve(_sql, placeholder, suggestedParameter);
            string label = placeholder.Kind == QueryParameterPlaceholderKind.Named
                ? placeholder.Token
                : placeholder.Token == "?"
                    ? $"? #{placeholder.Position}"
                    : placeholder.Token;

            TextBox input = new()
            {
                Watermark = hint.ExampleValue,
                Text = _initialValues.TryGetValue(placeholderKey, out string? remembered)
                    ? remembered
                    : FormatSuggestedValue(suggestedParameter),
            };
            _inputs[placeholder] = input;

            StackPanel fieldPanel = new()
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.Parse(UiColorConstants.C_E8EAED)),
                    },
                    new TextBlock
                    {
                        Text = $"{hint.TypeLabel} · {hint.Description}",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.Parse(UiColorConstants.C_9CA3AF)),
                    },
                },
            };

            if (!string.IsNullOrWhiteSpace(hint.ContextLabel))
            {
                fieldPanel.Children.Add(new TextBlock
                {
                    Text = hint.ContextLabel,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.Parse(UiColorConstants.C_60A5FA)),
                });
            }

            fieldPanel.Children.Add(input);
            rows.Children.Add(fieldPanel);
        }

        Button cancelButton = new()
        {
            Content = L("queryParameters.dialog.cancel", "Cancel"),
            MinWidth = 96,
            Padding = new Thickness(12, 6),
        };
        cancelButton.Click += (_, _) => Close();

        Button runButton = new()
        {
            Content = L("queryParameters.dialog.run", "Run preview"),
            MinWidth = 120,
            Padding = new Thickness(12, 6),
        };
        runButton.Click += (_, _) =>
        {
            EnteredValues = BuildEnteredValues();
            Result = BuildResult();
            Close();
        };

        return new Border
        {
            Padding = new Thickness(16),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = L("queryParameters.dialog.heading", "Provide parameter values"),
                                FontSize = 18,
                                FontWeight = FontWeight.SemiBold,
                                Foreground = new SolidColorBrush(Color.Parse(UiColorConstants.C_E8EAED)),
                            },
                            new TextBlock
                            {
                                Text = L("queryParameters.dialog.description", "These values will be used only for the current preview execution."),
                                TextWrapping = TextWrapping.Wrap,
                                Foreground = new SolidColorBrush(Color.Parse(UiColorConstants.C_9CA3AF)),
                            },
                        },
                    },
                    PlaceAtRow(new ScrollViewer
                    {
                        Margin = new Thickness(0, 12, 0, 12),
                        Content = rows,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    }, 1),
                    PlaceAtRow(new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            cancelButton,
                            runButton,
                        },
                    }, 2),
                },
            },
        };
    }

    private IReadOnlyList<QueryParameter> BuildResult()
    {
        List<QueryParameter> parameters = [];

        foreach (QueryParameterPlaceholder placeholder in _placeholders)
        {
            string raw = _inputs.TryGetValue(placeholder, out TextBox? input)
                ? input.Text?.Trim() ?? string.Empty
                : string.Empty;
            object? parsed = ParseInputValue(raw);

            parameters.Add(placeholder.Kind == QueryParameterPlaceholderKind.Named
                ? new QueryParameter(placeholder.Token, parsed)
                : new QueryParameter(null, parsed));
        }

        return parameters;
    }

    private IReadOnlyDictionary<string, string> BuildEnteredValues()
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        foreach (QueryParameterPlaceholder placeholder in _placeholders)
        {
            string raw = _inputs.TryGetValue(placeholder, out TextBox? input)
                ? input.Text?.Trim() ?? string.Empty
                : string.Empty;
            values[QueryParameterPlaceholderParser.GetStorageKey(placeholder)] = raw;
        }

        return values;
    }

    private static object? ParseInputValue(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        if (string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
            return null;
        if (bool.TryParse(raw, out bool boolValue))
            return boolValue;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
            return intValue;
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
            return longValue;
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal decimalValue))
            return decimalValue;
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dateTime))
            return dateTime;

        return raw;
    }

    private static string FormatSuggestedValue(QueryParameter? suggestedParameter)
    {
        if (suggestedParameter?.Value is null)
            return string.Empty;

        return suggestedParameter.Value switch
        {
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            bool boolValue => boolValue ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => suggestedParameter.Value.ToString() ?? string.Empty,
        };
    }

    private static Control PlaceAtRow(Control control, int row)
    {
        Grid.SetRow(control, row);
        return control;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        Close();
        e.Handled = true;
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
