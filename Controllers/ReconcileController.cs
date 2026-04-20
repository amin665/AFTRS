using Microsoft.AspNetCore.Mvc;
using AFTRS.Services;
using AFTRS.Data;
using Microsoft.EntityFrameworkCore;
using AFTRS.Models; 
namespace AFTRS.Controllers;

public class ReconcileController : Controller
{
    private readonly ReconciliationService _reconcileService;
    private readonly ApplicationDbContext _context;

    public ReconcileController(ReconciliationService reconcileService, ApplicationDbContext context)
    {
        _reconcileService = reconcileService;
        _context = context;
    }

    public async Task<IActionResult> Index()
{
    var activeBatch = await _context.ReconciliationBatches.FirstOrDefaultAsync(b => !b.IsFinalized);
    
    if (activeBatch == null) {
        return View(new List<Transaction>()); // Return empty list if no active batch
    }

    var transactions = await _context.Transactions
        .Where(t => t.BatchId == activeBatch.Id)
        .OrderByDescending(t => t.TransactionDate)
        .ToListAsync();
        
    return View(transactions);
}

    [HttpPost]
public async Task<IActionResult> RunEngine()
{
    // 1. Get the active batch
    var currentBatch = await _context.ReconciliationBatches
        .FirstOrDefaultAsync(b => !b.IsFinalized);

    if (currentBatch == null) return BadRequest("No active session found.");

    // 2. Pass the ID to the service
    await _reconcileService.RunReconciliationAsync(currentBatch.Id);
    
    TempData["Msg"] = "Reconciliation Engine finished processing.";
    return RedirectToAction("Index");
}
    [HttpPost]
public async Task<IActionResult> FinalizeBatch()
{
    var currentBatch = await _context.ReconciliationBatches
        .FirstOrDefaultAsync(b => !b.IsFinalized);

    if (currentBatch != null)
    {
        currentBatch.IsFinalized = true;
        await _context.SaveChangesAsync();
    }
    return RedirectToAction("Index", "Home"); // Go back to main dashboard
}
}