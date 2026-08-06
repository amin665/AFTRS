using AFTRS.Data;
using AFTRS.Infrastructure;
using AFTRS.Models;
using AFTRS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

[RoleAuthorize("Manager", "Admin")]
[PermissionAuthorize(AppPermissions.Import)]
public class ImportController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ImportService _import;
    private readonly ReconciliationSessionContext _sessions;

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

    public ImportController(ApplicationDbContext context, ImportService import, ReconciliationSessionContext sessions)
    {
        _context = context;
        _import = import;
        _sessions = sessions;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var session = await _sessions.GetSelectedAsync();
        ViewBag.Session = session;
        ViewBag.UploadHistory = await _context.FileUploadRecords
            .Where(r => r.SessionID == session.SessionID)
            .OrderByDescending(r => r.UploadedAt)
            .Take(25)
            .ToListAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file, string sourceType)
    {
        var session = await _sessions.GetSelectedAsync();
        if (session.Status != "Active")
        {
            TempData["Error"] = UiText.T(Request, "ArchivedSessionReadOnly");
            return RedirectToAction(nameof(Index));
        }

        if (file == null || file.Length == 0)
        {
            LogUploadAttempt(false, sourceType);
            await _context.SaveChangesAsync();
            TempData["Error"] = UiText.T(Request, "NoFileUploaded");
            return RedirectToAction(nameof(Index));
        }

        if (file.Length > MaxFileSizeBytes)
        {
            LogUploadAttempt(false, sourceType);
            await _context.SaveChangesAsync();
            TempData["Error"] = string.Format(UiText.T(Request, "FileTooLarge"), file.FileName);
            return RedirectToAction(nameof(Index));
        }

        if (sourceType != "Ledger" && sourceType != "Bank")
        {
            LogUploadAttempt(false, sourceType);
            await _context.SaveChangesAsync();
            TempData["Error"] = UiText.T(Request, "InvalidSourceType");
            return RedirectToAction(nameof(Index));
        }

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        var hash = ImportService.ComputeSha256(bytes);
        var hashError = await _import.CheckFileHashAsync(session.SessionID, file.FileName, hash);
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
            result = await _import.ParseCsvWithValidation(stream, sourceType, session.SessionID);
        else if (ext == ".xlsx")
            result = await _import.ParseExcelWithValidation(stream, sourceType, session.SessionID);
        else
        {
            LogUploadAttempt(false, sourceType);
            await _context.SaveChangesAsync();
            TempData["Error"] = UiText.T(Request, "InvalidFileType");
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
            TempData["Error"] = UiText.T(Request, "NoNewRows");
            return RedirectToAction(nameof(Index));
        }

        _context.Transactions.AddRange(result.Data);
        _import.RecordFileUpload(session.SessionID, file.FileName, hash, sourceType);

        // Security log for upload attempt (FR-04 style outcome logging extended to uploads).
        LogUploadAttempt(true, sourceType);

        await _context.SaveChangesAsync();

        TempData["Success"] = string.Format(UiText.T(Request, "ImportedRecords"), result.Data.Count, file.FileName, sourceType);
        return RedirectToAction(nameof(Index));
    }
}
