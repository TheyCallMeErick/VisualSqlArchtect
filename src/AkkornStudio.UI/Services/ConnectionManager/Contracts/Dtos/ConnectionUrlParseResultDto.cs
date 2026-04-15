using System.Collections.Generic;

namespace AkkornStudio.UI.Services.ConnectionManager.Contracts;

public sealed record ConnectionUrlParseResultDto(
    ConnectionUrlParseStatusDto ParseStatus,
    IReadOnlyList<UrlParseFieldTokenDto> RecognizedFields,
    IReadOnlyList<string> UnrecognizedTokens,
    string? SuggestedProvider,
    bool ConflictWithSelectedProvider,
    string? NormalizedUrl,
    string UserMessage,
    string? TechnicalDetails);
