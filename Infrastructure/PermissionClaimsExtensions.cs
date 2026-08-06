using System.Security.Claims;

namespace AFTRS.Infrastructure;

public static class PermissionClaimsExtensions
{
    public static bool HasPermission(this ClaimsPrincipal? user, string permission)
    {
        if (user?.Identity?.IsAuthenticated != true) return false;
        if (user.HasClaim(AuthConstants.RoleClaimType, "Admin")) return true;
        return user.HasClaim(AuthConstants.PermissionClaimType, permission);
    }
}
