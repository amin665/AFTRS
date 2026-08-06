using AFTRS.Data;
using AFTRS.Infrastructure;
using AFTRS.Models;
using AFTRS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

[RoleAuthorize("Manager", "Admin")]
[PermissionAuthorize(AppPermissions.Sessions)]
public class SessionsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ReconciliationSessionContext _sessions;

    public SessionsController(ApplicationDbContext context, ReconciliationSessionContext sessions)
    {
        _context = context;
        _sessions = sessions;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var selected = await _sessions.GetSelectedAsync();
        ViewBag.SelectedSession = selected;
        ViewBag.Sessions = await _context.ReconciliationSessions
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
        ViewBag.TransactionCounts = await _context.Transactions
            .GroupBy(t => t.SessionID)
            .Select(g => new { SessionID = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionID, x => x.Count);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Select(int sessionId)
    {
        var session = await _context.ReconciliationSessions.FindAsync(sessionId);
        if (session == null) return NotFound();

        _sessions.Select(session.SessionID);
        TempData["Msg"] = string.Format(UiText.T(Request, "SessionSelected"), session.Name);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RoleAuthorize("Admin")]
    public async Task<IActionResult> ArchiveAndStart(string? name)
    {
        var current = await _sessions.GetSelectedAsync();
        if (current.Status != "Active")
        {
            TempData["Error"] = UiText.T(Request, "OnlyActiveSessionArchive");
            return RedirectToAction(nameof(Index));
        }

        var userId = User.FindFirst(AuthConstants.UserIdClaimType)?.Value;
        current.Status = "Archived";
        current.ArchivedAt = DateTime.Now;
        current.ArchivedByUserID = userId == null ? null : int.Parse(userId);

        var newSession = new ReconciliationSession
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Session {DateTime.Now:yyyy-MM-dd HH:mm}" : name.Trim(),
            CreatedAt = DateTime.Now,
            CreatedByUserID = userId == null ? null : int.Parse(userId)
        };
        _context.ReconciliationSessions.Add(newSession);
        await _context.SaveChangesAsync();

        _sessions.Select(newSession.SessionID);
        TempData["Msg"] = UiText.T(Request, "NewSessionStarted");
        return RedirectToAction(nameof(Index));
    }
}
