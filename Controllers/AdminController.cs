using AFTRS.Data;
using AFTRS.Infrastructure;
using AFTRS.Models;
using AFTRS.Services;
using AFTRS.ViewModels;
using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

[RoleAuthorize("Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ReconciliationSessionContext _sessions;

    public AdminController(ApplicationDbContext context, ReconciliationSessionContext sessions)
    {
        _context = context;
        _sessions = sessions;
    }

    private static bool IsPasswordComplexEnough(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8) return false;
        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSymbol = password.Any(ch => !char.IsLetterOrDigit(ch));
        return hasUpper && hasLower && hasDigit && hasSymbol;
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var currentUserId = GetCurrentUserId();
        var model = new AdminUsersViewModel
        {
            Users = await _context.Users
                .Include(u => u.Permissions)
                .Where(u => currentUserId == null || u.UserID != currentUserId.Value)
                .OrderBy(u => u.Username)
                .ToListAsync()
        };
        return View(model);
    }

    [HttpGet]
    public IActionResult CreateUser()
    {
        return View(new AdminCreateUserViewModel { Permissions = AppPermissions.All.Select(p => p.Key).ToList() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(AdminCreateUserViewModel model)
    {
        if (!string.Equals(model.Role, "Admin", StringComparison.OrdinalIgnoreCase) && !string.Equals(model.Role, "Manager", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError("Role", UiText.T(Request, "InvalidRole"));

        if (!IsPasswordComplexEnough(model.Password))
            ModelState.AddModelError("", UiText.T(Request, "PasswordComplexity"));

        if (await _context.Users.AnyAsync(u => u.Username == model.Email))
            ModelState.AddModelError("", UiText.T(Request, "UsernameExists"));

        if (!ModelState.IsValid) return View(model);

        var role = string.Equals(model.Role, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Manager";
        var user = new User
        {
            Username = model.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            Role = role,
            IsLocked = false
        };

        if (user.Role == "Manager")
        {
            foreach (var permission in model.Permissions.Where(AppPermissions.IsValid).Distinct(StringComparer.OrdinalIgnoreCase))
                user.Permissions.Add(new UserPermission { Permission = permission });
        }

        _context.Users.Add(user);
        await AddSecurityEventAsync("CreateUser");
        await _context.SaveChangesAsync();

        TempData["Msg"] = UiText.T(Request, "UserCreated");
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> EditUserPermissions(int id)
    {
        if (id == GetCurrentUserId()) return Forbid();

        var user = await _context.Users.Include(u => u.Permissions).FirstOrDefaultAsync(u => u.UserID == id);
        if (user == null) return NotFound();

        return View(new AdminEditUserPermissionsViewModel
        {
            UserID = user.UserID,
            Email = user.Username,
            Role = user.Role,
            IsLocked = user.IsLocked,
            Permissions = user.Permissions.Select(p => p.Permission).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUserPermissions(AdminEditUserPermissionsViewModel model)
    {
        if (model.UserID == GetCurrentUserId()) return Forbid();

        var user = await _context.Users.Include(u => u.Permissions).FirstOrDefaultAsync(u => u.UserID == model.UserID);
        if (user == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            if (!IsPasswordComplexEnough(model.NewPassword))
            {
                ModelState.AddModelError("", UiText.T(Request, "PasswordComplexity"));
                model.Email = user.Username;
                model.Role = user.Role;
                model.IsLocked = user.IsLocked;
                model.Permissions = user.Permissions.Select(p => p.Permission).ToList();
                return View(model);
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            await AddSecurityEventAsync("ChangeUserPassword");
        }

        user.Permissions.Clear();
        if (user.Role == "Manager")
        {
            foreach (var permission in model.Permissions.Where(AppPermissions.IsValid).Distinct(StringComparer.OrdinalIgnoreCase))
                user.Permissions.Add(new UserPermission { UserID = user.UserID, Permission = permission });
        }

        await AddSecurityEventAsync("UpdateUserPermissions");
        await _context.SaveChangesAsync();

        TempData["Msg"] = UiText.T(Request, "PermissionsSaved");
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LockUser(int id)
    {
        if (id == GetCurrentUserId()) return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.IsLocked = true;
        await AddSecurityEventAsync("LockUser");
        await _context.SaveChangesAsync();

        TempData["Msg"] = UiText.T(Request, "UserLocked");
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlockUser(int id)
    {
        if (id == GetCurrentUserId()) return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.IsLocked = false;
        await AddSecurityEventAsync("UnlockUser");
        await _context.SaveChangesAsync();

        TempData["Msg"] = UiText.T(Request, "UserUnlocked");
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> SecurityLogs(DateTime? date, int? userId)
    {
        var query = _context.SecurityLogs.Include(l => l.User).AsQueryable();
        if (date.HasValue)
        {
            var start = date.Value.Date;
            var end = start.AddDays(1);
            query = query.Where(l => l.Timestamp >= start && l.Timestamp < end);
        }
        if (userId.HasValue)
            query = query.Where(l => l.UserID == userId.Value);

        ViewBag.Users = await _context.Users.OrderBy(u => u.Username).ToListAsync();
        ViewBag.SelectedDate = date?.ToString("yyyy-MM-dd");
        ViewBag.SelectedUserId = userId;

        var logs = await query.OrderByDescending(l => l.Timestamp).ToListAsync();

        return View(logs);
    }

    [HttpGet]
    public async Task<IActionResult> AuditTrail()
    {
        var session = await _sessions.GetSelectedAsync();
        ViewBag.Session = session;
        var trail = await _context.FinancialAuditLogs
            .Where(a => a.SessionID == session.SessionID)
            .Include(a => a.User)
            .Include(a => a.Transaction)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();

        return View(trail);
    }

    private async Task AddSecurityEventAsync(string action)
    {
        var userId = GetCurrentUserId();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        await _context.SecurityLogs.AddAsync(new SecurityLog { UserID = userId, IPAddress = ip, Action = action, IsSuccess = true });
    }

    private int? GetCurrentUserId()
    {
        var userIdValue = User.FindFirst(AuthConstants.UserIdClaimType)?.Value;
        return int.TryParse(userIdValue, out var parsed) ? parsed : null;
    }
}
