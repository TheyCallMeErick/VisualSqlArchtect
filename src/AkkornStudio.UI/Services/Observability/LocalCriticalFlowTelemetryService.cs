using System.Text.Json;

namespace AkkornStudio.UI.Services.Observability;

public sealed class LocalCriticalFlowTelemetryService : ICriticalFlowTelemetryService
{
    private const string ProductFolderName = "AkkornStudio";
    private const string TelemetryFolderName = "telemetry";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly string _logDirectory;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly object _gate = new();

    public LocalCriticalFlowTelemetryService()
        : this(logDirectory: null, utcNow: null)
    {
    }

    public LocalCriticalFlowTelemetryService(string? logDirectory, Func<DateTimeOffset>? utcNow)
    {
        _logDirectory = string.IsNullOrWhiteSpace(logDirectory)
            ? ResolveDefaultDirectory()
            : logDirectory;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        SessionId = Guid.NewGuid().ToString("N");
    }

    public string SessionId { get; }

    public void Track(
        string flowId,
        string step,
        string outcome,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        var evt = new CriticalFlowEvent(
            SessionId,
            _utcNow(),
            flowId,
            step,
            outcome,
            properties ?? new Dictionary<string, object?>());

        string line = JsonSerializer.Serialize(evt, JsonOptions);
        string filePath = Path.Combine(_logDirectory, $"critical-flows-{evt.TimestampUtc:yyyy-MM-dd}.jsonl");

        lock (_gate)
        {
            Directory.CreateDirectory(_logDirectory);
            File.AppendAllText(filePath, line + Environment.NewLine);
        }
    }

    private static string ResolveDefaultDirectory()
    {
        string baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
            baseDirectory = AppContext.BaseDirectory;

        return Path.Combine(baseDirectory, ProductFolderName, TelemetryFolderName);
    }
}
