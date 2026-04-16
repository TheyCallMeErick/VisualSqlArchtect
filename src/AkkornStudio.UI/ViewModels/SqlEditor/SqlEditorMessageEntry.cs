namespace AkkornStudio.UI.ViewModels;

public sealed record SqlEditorMessageEntry(
    DateTimeOffset Timestamp,
    string Source,
    string Title,
    string? Detail,
    bool IsError,
    string? Sql)
{
    public string TimestampText => Timestamp.ToString("dd/MM/yyyy HH:mm:ss zzz");
    public string LevelText => IsError ? "ERRO" : "INFO";
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);
    public bool HasSql => !string.IsNullOrWhiteSpace(Sql);
}
