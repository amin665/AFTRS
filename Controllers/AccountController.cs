using System.Security.Claims;
using AFTRS.Data;
using AFTRS.Infrastructure;
using AFTRS.Models;
using AFTRS.Services;
using AFTRS.ViewModels;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuthService _auth;

    private const int MaxFailedAttempts = 5;

    public AccountController(ApplicationDbContext context, AuthService auth)
    {
        _context = context;
        _auth = auth;
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        // If username exists, enforce lockout after 5 failed attempts.
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Email);
        if (user != null && user.IsLocked)
        {
            await _context.SecurityLogs.AddAsync(new SecurityLog { UserID = user.UserID, IPAddress = ip, Action = "Login", IsSuccess = false });
            await _context.SaveChangesAsync();
            ModelState.AddModelError("", "Account locked. Contact the System Administrator.");
            return View(model);
        }

        // Count recent consecutive failed attempts (in-memory rule; no separate table in schema).
        var recentFailures = await _context.SecurityLogs
            .Where(l => l.Action == "Login" && l.IsSuccess == false && l.UserID == (user != null ? user.UserID : null))
            .OrderByDescending(l => l.Timestamp)
            .Take(MaxFailedAttempts)
            .ToListAsync();

        if (user != null && recentFailures.Count == MaxFailedAttempts)
        {
            // Already failed 5 times.
            user.IsLocked = true;
            await _context.SecurityLogs.AddAsync(new SecurityLog { UserID = user.UserID, IPAddress = ip, Action = "Login", IsSuccess = false });
            await _context.SaveChangesAsync();
            ModelState.AddModelError("", "Account locked. Too many failed attempts.");
            return View(model);
        }

        var (validated, isLocked) = await _auth.ValidateCredentialsAsync(model.Email, model.Password);

        if (isLocked)
        {
            ModelState.AddModelError("", "Account locked. Contact the System Administrator.");
            return View(model);
        }

        if (validated == null)
        {
            // Write security log (if user exists, link it).
            await _context.SecurityLogs.AddAsync(new SecurityLog { UserID = user?.UserID, IPAddress = ip, Action = "Login", IsSuccess = false });

            if (user != null)
            {
                // Lock on 5th failure.
                var failureCount = await _context.SecurityLogs.CountAsync(l => l.Action == "Login" && l.IsSuccess == false && l.UserID == user.UserID);
                if (failureCount + 1 >= MaxFailedAttempts)
                    user.IsLocked = true;
            }

            await _context.SaveChangesAsync();
            ModelState.AddModelError("", "Invalid login attempt.");
            return View(model);
        }

        await _context.SecurityLogs.AddAsync(new SecurityLog { UserID = validated.UserID, IPAddress = ip, Action = "Login", IsSuccess = true });
        await _context.SaveChangesAsync();

        var principal = AuthService.CreatePrincipal(validated);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = model.RememberMe });

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Only allow Manager/Admin as per schema.
        var role = model.Role == "Admin" ? "Admin" : "Manager";

        var exists = await _context.Users.AnyAsync(u => u.Username == model.Email);
        if (exists)
        {
            ModelState.AddModelError("", "Username already exists.");
            return View(model);
        }

        var user = new User
        {
            Username = model.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            Role = role,
            IsLocked = false
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return RedirectToAction("Login");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
