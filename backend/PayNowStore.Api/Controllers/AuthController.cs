using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayNowStore.Api.Data;
using PayNowStore.Api.Dtos;
using PayNowStore.Api.Models;
using PayNowStore.Api.Services;

namespace PayNowStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext dbContext, JwtTokenService jwtTokenService) : ControllerBase
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Surname) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.PaynowIntegrationId) ||
            string.IsNullOrWhiteSpace(request.PaynowIntegrationKey))
        {
            return BadRequest(new { message = "All registration fields must be filled in." });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(u => u.Email == email))
        {
            return BadRequest(new { message = "An account with that email already exists." });
        }

        var name = request.Name.Trim();
        var surname = request.Surname.Trim();
        var fullName = $"{name} {surname}".Trim();
        var user = new User
        {
            Name = name,
            Surname = surname,
            FullName = fullName,
            Email = email,
            Role = "Customer",
            PaynowIntegrationId = request.PaynowIntegrationId.Trim(),
            PaynowIntegrationKey = request.PaynowIntegrationKey.Trim(),
            LastLoginAtUtc = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return Ok(BuildResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return Ok(BuildResponse(user));
    }

    private AuthResponse BuildResponse(User user) =>
        new(
            jwtTokenService.Generate(user),
            new UserDto(user.Id, user.Name, user.Surname, user.FullName, user.Email, user.Role, user.LastLoginAtUtc));
}
