namespace PayNowStore.Api.Dtos;

public record RegisterRequest(
    string Name,
    string Surname,
    string Email,
    string Password,
    string PaynowIntegrationId,
    string PaynowIntegrationKey);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, UserDto User);
public record UserDto(int Id, string Name, string Surname, string FullName, string Email, string Role, DateTime? LastLoginAtUtc);
