using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace AFTRS.Infrastructure;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class PermissionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    private readonly HashSet<string> _permissions;

    public PermissionAuthorizeAttribute(params string[] permissions)
    {
        _permissions = permissions.Select(p => p.Trim()).Where(p => p.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        var role = user.FindFirstValue(AuthConstants.RoleClaimType);
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) return;

        var granted = user.FindAll(AuthConstants.PermissionClaimType).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!_permissions.Any(granted.Contains))
            context.Result = new ForbidResult();
    }
}
