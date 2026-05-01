using System.Security.Claims;

namespace PayNowStore.Api.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor)
{
    public int GetUserId()
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

        return int.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Authenticated user id not found.");
    }
}
