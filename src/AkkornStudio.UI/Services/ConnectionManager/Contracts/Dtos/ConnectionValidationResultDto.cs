using System.Collections.Generic;

namespace AkkornStudio.UI.Services.ConnectionManager.Contracts;

public sealed record ConnectionValidationResultDto(
    bool IsValid,
    IReadOnlyList<ConnectionValidationMessageDto> Errors,
    IReadOnlyList<ConnectionValidationMessageDto> Warnings);
