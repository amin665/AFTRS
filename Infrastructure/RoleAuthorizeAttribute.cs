using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace AFTRS.Infrastructure;

// Minimal role filter so we don't depend on Identity role infrastructure.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RoleAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    private readonly HashSet<string> _roles;

    public RoleAuthorizeAttribute(params string[] roles)
    {
        _roles = roles.Select(r => r.Trim()).Where(r => r.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
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
        if (role == null || !_roles.Contains(role))
        {
            context.Result = new ForbidResult();
        }
    }
}
