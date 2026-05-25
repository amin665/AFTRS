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
    public async Task<IActionResult> SecurityLogs()
    {
        var logs = await _context.SecurityLogs
            .Include(l => l.User)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();

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
