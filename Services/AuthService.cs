using System.Security.Claims;
using AFTRS.Data;
using AFTRS.Infrastructure;
using AFTRS.Models;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Services;

public class AuthService
{
    private readonly ApplicationDbContext _context;

    public AuthService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(User? User, bool IsLocked)> ValidateCredentialsAsync(string username, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return (null, false);
        if (user.IsLocked) return (null, true);

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return (null, false);

        return (user, false);
    }

    public static ClaimsPrincipal CreatePrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(AuthConstants.UserIdClaimType, user.UserID.ToString()),
            new(AuthConstants.RoleClaimType, user.Role)
        };

        var identity = new ClaimsIdentity(claims, AuthConstants.Scheme);
        return new ClaimsPrincipal(identity);
    }
}
