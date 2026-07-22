using AFTRS.Data;
using AFTRS.Infrastructure;
using AFTRS.Models;
using AFTRS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

[RoleAuthorize("Manager", "Admin")]
public class ImportController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ImportService _import;

    private const long MaxFileSizeBytes = 25L * 1024L * 1024L;

    private void LogUploadAttempt(bool isSuccess, string sourceType)
    {
        int? userId = null;
        var uid = User.FindFirst(AuthConstants.UserIdClaimType)?.Value;
        if (uid != null) userId = int.Parse(uid);

        var action = sourceType == "Ledger" || sourceType == "Bank" ? $"Upload-{sourceType}" : "Upload";
        _context.SecurityLogs.Add(new SecurityLog
        {
            UserID = userId,
            IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            Action = action,
            IsSuccess = isSuccess,
            Timestamp = DateTime.Now
        });
    }

    public ImportController(ApplicationDbContext context, ImportService import)
    {
        _context = context;
        _import = import;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.UploadHistory = await _context.FileUploadRecords
            .OrderByDescending(r => r.UploadedAt)
            .Take(25)
            .ToListAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file, string sourceType)
    {
        if (file == null || file.Length == 0)
        {
            LogUploadAttempt(false, sourceType);
            await _context.SaveChangesAsync();
            return BadRequest("No file uploaded.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            LogUploadAttempt(false, sourceType);
            await _context.SaveChangesAsync();
            TempData["Error"] = $"File '{file.FileName}' exceeds the 25 MB size limit.";
            return RedirectToAction(nameof(Index));
        }

        if (sourceType != "Ledger" && sourceType != "Bank")
        {
            LogUploadAttempt(false, sourceType);
            await _context.SaveChangesAsync();
            TempData["Error"] = "Invalid source type.";
            return RedirectToAction(nameof(Index));
        }

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        var hash = ImportService.ComputeSha256(bytes);
        var hashError = await _import.CheckFileHashAsync(file.FileName, hash);
        if (hashError != null)
        {
            LogUploadAttempt(false, sourceType);
            await _context.SaveChangesAsync();
            TempData["Error"] = hashError;
            return RedirectToAction(nameof(Index));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        (List<Transaction> Data, string? Error) result;
        using var stream = new MemoryStream(bytes);

        if (ext == ".csv")
            result = await _import.ParseCsvWithValidation(stream, sourceType);
        else if (ext == ".xlsx")
            result = await _import.ParseExcelWithValidation(stream, sourceType);
        else
        {
            LogUploadAttempt(false, sourceType);
            await _context.SaveChangesAsync();
            TempData["Error"] = "Invalid file type. Only .csv and .xlsx are supported.";
            return RedirectToAction(nameof(Index));
        }

        if (result.Error != null)
        {
            // If it's an Unbalanced warning, treat it as non-fatal.
            if (result.Error.Contains("UNBALANCED", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Warning"] = result.Error;
            }
            else
            {
                LogUploadAttempt(false, sourceType);
                await _context.SaveChangesAsync();
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Index));
            }
        }

        if (result.Data.Count == 0)
        {
            LogUploadAttempt(false, sourceType);
            await _context.SaveChangesAsync();
            TempData["Error"] = "No new rows imported (all rows may be duplicates).";
            return RedirectToAction(nameof(Index));
        }

        _context.Transactions.AddRange(result.Data);
        _import.RecordFileUpload(file.FileName, hash, sourceType);

        // Security log for upload attempt (FR-04 style outcome logging extended to uploads).
        LogUploadAttempt(true, sourceType);

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Imported {result.Data.Count} records from '{file.FileName}' ({sourceType}).";
        return RedirectToAction(nameof(Index));
    }
}
