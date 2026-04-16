namespace AkkornStudio.UI.Services.ConnectionManager.Contracts;

public sealed record ConnectionTestResultDto(
    ConnectionTestStatusDto Status,
    string SummaryMessage,
    string? TechnicalDetails,
    int? LatencyMs,
    string? ProviderErrorCode,
    DateTimeOffset TestedAt);
