namespace PayNowStore.Api.Services;

public record PaynowInitiationResult(bool Success, string RedirectUrl, string PollUrl, string? Error);
public record PaynowStatusResult(bool Success, string Status, string? PaynowReference, string? PollUrl, string? Error);
