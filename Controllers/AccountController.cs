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

    private static bool IsPasswordComplexEnough(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8) return false;
        bool hasUpper = false, hasLower = false, hasDigit = false, hasSymbol = false;
        foreach (var ch in password)
        {
            if (char.IsUpper(ch)) hasUpper = true;
            else if (char.IsLower(ch)) hasLower = true;
            else if (char.IsDigit(ch)) hasDigit = true;
            else hasSymbol = true;
        }
        return hasUpper && hasLower && hasDigit && hasSymbol;
    }

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
            ModelState.AddModelError("", UiText.T(Request, "AccountLockedAdmin"));
            return View(model);
        }

        if (user != null && user.FailedLoginAttempts >= MaxFailedAttempts)
        {
            user.IsLocked = true;
            await _context.SecurityLogs.AddAsync(new SecurityLog { UserID = user.UserID, IPAddress = ip, Action = "Login", IsSuccess = false });
            await _context.SaveChangesAsync();
            ModelState.AddModelError("", UiText.T(Request, "AccountLockedAttempts"));
            return View(model);
        }

        var (validated, isLocked) = await _auth.ValidateCredentialsAsync(model.Email, model.Password);

        if (isLocked)
        {
            ModelState.AddModelError("", UiText.T(Request, "AccountLockedAdmin"));
            return View(model);
        }

        if (validated == null)
        {
            // Write security log (if user exists, link it).
            await _context.SecurityLogs.AddAsync(new SecurityLog { UserID = user?.UserID, IPAddress = ip, Action = "Login", IsSuccess = false });

            if (user != null)
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= MaxFailedAttempts)
                    user.IsLocked = true;
            }

            await _context.SaveChangesAsync();
            ModelState.AddModelError("", UiText.T(Request, "InvalidLogin"));
            return View(model);
        }

        await _context.SecurityLogs.AddAsync(new SecurityLog { UserID = validated.UserID, IPAddress = ip, Action = "Login", IsSuccess = true });
        validated.FailedLoginAttempts = 0;
        await _context.SaveChangesAsync();

        var principal = AuthService.CreatePrincipal(validated);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = model.RememberMe });

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public async Task<IActionResult> Register()
    {
        if (await _context.Users.AnyAsync(u => u.Role == "Admin"))
            return RedirectToAction(nameof(Login));

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (await _context.Users.AnyAsync(u => u.Role == "Admin"))
            return Forbid();

        if (!ModelState.IsValid) return View(model);

        if (!IsPasswordComplexEnough(model.Password))
        {
            ModelState.AddModelError("", UiText.T(Request, "PasswordComplexity"));
            return View(model);
        }

        var exists = await _context.Users.AnyAsync(u => u.Username == model.Email);
        if (exists)
        {
            ModelState.AddModelError("", UiText.T(Request, "UsernameExists"));
            return View(model);
        }

        var user = new User
        {
            Username = model.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            Role = "Admin",
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
