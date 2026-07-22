using AFTRS.Data;
using AFTRS.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

[RoleAuthorize("Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;

    public AdminController(ApplicationDbContext context)
    {
        _context = context;
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
        var trail = await _context.FinancialAuditLogs
            .Include(a => a.User)
            .Include(a => a.Transaction)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();

        return View(trail);
    }
}
