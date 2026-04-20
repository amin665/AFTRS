using Microsoft.AspNetCore.Mvc;
using AFTRS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using AFTRS.Models;
namespace AFTRS.Controllers;

[Authorize(Roles = "Admin")] // Only Admins can see these logs
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;

    public AdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    // UC-15: Security Logs
    public async Task<IActionResult> SecurityLogs()
    {
        var logs = await _context.SecurityLogs.OrderByDescending(l => l.Timestamp).ToListAsync();
        return View(logs);
    }

    // UC-16: Financial Audit Trail
    public async Task<IActionResult> AuditTrail()
    {
        var trail = await _context.FinancialAuditLogs.OrderByDescending(a => a.Timestamp).ToListAsync();
        return View(trail);
    }
    [HttpPost]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> UndoMatch(int transactionId, string reason)
{
    if (string.IsNullOrEmpty(reason)) return BadRequest("Reason for correction is required.");

    // 1. Find the transaction and its partner
    var t1 = await _context.Transactions.FindAsync(transactionId);
    if (t1 == null || t1.MatchedTransactionId == null) return NotFound();

    var t2 = await _context.Transactions.FindAsync(t1.MatchedTransactionId);

    // 2. Revert status and break link
    t1.Status = "Unmatched";
    t1.MatchedTransactionId = null;

    if (t2 != null)
    {
        t2.Status = "Unmatched";
        t2.MatchedTransactionId = null;
    }

    // 3. Log this correction in the Audit Trail
    var log = new FinancialAuditLog
    {
        Action = "Admin Correction (Undo Match)",
        Justification = $"UNDO: {reason}",
        UserEmail = User.Identity?.Name ?? "Admin",
        BatchName = "Correction System",
        Timestamp = DateTime.Now
    };

    _context.FinancialAuditLogs.Add(log);
    await _context.SaveChangesAsync();

    return RedirectToAction("AuditTrail");
}
}