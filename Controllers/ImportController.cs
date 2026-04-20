using Microsoft.AspNetCore.Mvc;
using AFTRS.Data;
using AFTRS.Models;
using AFTRS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace AFTRS.Controllers;

[Authorize(Roles = "FinancialManager,Admin")]
public class ImportController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ImportService _importService;

    public ImportController(ApplicationDbContext context, ImportService importService)
    {
        _context = context;
        _importService = importService;
    }

    // GET: /Import
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var activeBatch = await _context.ReconciliationBatches
            .FirstOrDefaultAsync(b => !b.IsFinalized);
        
        ViewBag.ActiveBatchName = activeBatch?.Name;
        return View();
    }

    // POST: /Import/CreateBatch
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBatch(string batchName)
    {
        if (string.IsNullOrWhiteSpace(batchName))
        {
            return RedirectToAction(nameof(Index));
        }

        var activeBatch = await _context.ReconciliationBatches
            .FirstOrDefaultAsync(b => !b.IsFinalized);

        if (activeBatch == null)
        {
            var newBatch = new ReconciliationBatch { Name = batchName };
            _context.ReconciliationBatches.Add(newBatch);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /Import/Upload
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file, string sourceType)
    {
        var currentBatch = await _context.ReconciliationBatches
            .FirstOrDefaultAsync(b => !b.IsFinalized);
        
        if (currentBatch == null) return BadRequest("No active session found.");
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

        using var stream = file.OpenReadStream();
        var extension = Path.GetExtension(file.FileName).ToLower();
        
        // Use the new Tuple-based result from the updated ImportService
        (List<Transaction> Data, string? Error) result;

        if (extension == ".csv")
        {
            result = await _importService.ParseCsvWithValidation(stream, sourceType);
        }
        else if (extension == ".xlsx")
        {
            result = await _importService.ParseExcelWithValidation(stream, sourceType);
        }
        else
        {
            TempData["Error"] = "Invalid file type. Only .csv and .xlsx are supported.";
            return RedirectToAction(nameof(Index));
        }

        // Handle validation errors (like duplicates)
        if (result.Error != null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        // Process successful imports
        foreach (var t in result.Data)
        {
            t.BatchId = currentBatch.Id;
        }

        _context.Transactions.AddRange(result.Data);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Successfully imported {result.Data.Count} records to {sourceType}.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Import/ClearCurrentBatch
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearCurrentBatch()
    {
        var currentBatch = await _context.ReconciliationBatches
            .FirstOrDefaultAsync(b => !b.IsFinalized);

        if (currentBatch != null)
        {
            var transactions = _context.Transactions.Where(t => t.BatchId == currentBatch.Id);
            _context.Transactions.RemoveRange(transactions);
            _context.ReconciliationBatches.Remove(currentBatch);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}