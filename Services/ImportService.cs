using System.Globalization;
using System.Security.Cryptography;
using AFTRS.Models;
using AFTRS.Data;
using CsvHelper;
using MiniExcelLibs;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Services;

public class ImportService
{
    private readonly ApplicationDbContext _context;

    public ImportService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>Computes SHA-256 hex digest of a byte array (FR-09a).</summary>
    public static string ComputeSha256(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Checks whether this file (by name + SHA-256 hash) has already been uploaded (FR-09a).
    /// </summary>
    public async Task<string?> CheckFileHashAsync(string fileName, string fileHash, string source, int batchId)
    {
        var exists = await _context.FileUploadRecords
            .AnyAsync(r => r.FileHash == fileHash || r.FileName == fileName);
        if (exists)
            return $"Duplicate file detected: '{fileName}' (or a file with identical content) has already been uploaded. Upload aborted to prevent data duplication.";
        return null;
    }

    /// <summary>Records a file upload entry after successful import (FR-09a).</summary>
    public void RecordFileUpload(string fileName, string fileHash, string source, string? uploadedBy, int batchId)
    {
        _context.FileUploadRecords.Add(new FileUploadRecord
        {
            FileName = fileName,
            FileHash = fileHash,
            Source = source,
            UploadedBy = uploadedBy,
            BatchId = batchId,
            UploadedAt = DateTime.Now
        });
    }

    /// <summary>
    /// FR-09b: Checks individual transaction rows against existing records
    /// using (ReferenceNumber + Date + Amount) to skip duplicates.
    /// </summary>
    private async Task<List<Transaction>> FilterDuplicateRowsAsync(List<Transaction> newTransactions, string source)
    {
        // Pull existing (Ref, Date, Amount) combos for this source
        var existing = await _context.Transactions
            .Where(t => t.Source == source && t.ReferenceNumber != null)
            .Select(t => new { t.ReferenceNumber, t.TransactionDate, t.Amount })
            .ToListAsync();

        var existingSet = existing
            .Select(e => $"{e.ReferenceNumber}|{e.TransactionDate:yyyy-MM-dd}|{e.Amount}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return newTransactions
            .Where(t => string.IsNullOrEmpty(t.ReferenceNumber) ||
                        !existingSet.Contains($"{t.ReferenceNumber}|{t.TransactionDate:yyyy-MM-dd}|{t.Amount}"))
            .ToList();
    }

    public async Task<(List<Transaction> Data, string? Error)> ParseCsvWithValidation(Stream fileStream, string source)
    {
        using var reader = new StreamReader(fileStream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        try
        {
            var records = csv.GetRecords<dynamic>().Select(row =>
            {
                var t = new Transaction
                {
                    TransactionDate = DateTime.Parse(row.Date),
                    Description = row.Description,
                    ReferenceNumber = row.Reference,
                    Amount = decimal.Parse(row.Amount),
                    Source = source,
                    Status = "Unmatched"
                };
                // Optional Type column (Credit/Debit) — SRS 3.2.2
                var dict = (IDictionary<string, object>)row;
                if (dict.TryGetValue("Type", out var typeVal) && typeVal != null)
                    t.TransactionType = typeVal.ToString();
                return t;
            }).ToList();

            var filtered = await FilterDuplicateRowsAsync(records, source);
            return (filtered, null);
        }
        catch (Exception)
        {
            return (new List<Transaction>(), "Error parsing CSV. Please ensure columns are: Date, Description, Reference, Amount. Type is optional.");
        }
    }

    public async Task<(List<Transaction> Data, string? Error)> ParseExcelWithValidation(Stream fileStream, string source)
    {
        try
        {
            var rows = fileStream.Query(useHeaderRow: true).ToList();
            var records = rows.Select(row =>
            {
                var dict = (IDictionary<string, object>)row;
                string? typeVal = null;
                if (dict.TryGetValue("Type", out var tv) && tv != null)
                    typeVal = tv.ToString();

                return new Transaction
                {
                    TransactionDate = Convert.ToDateTime(row.Date),
                    Description = Convert.ToString(row.Description) ?? string.Empty,
                    ReferenceNumber = Convert.ToString(row.Reference),
                    Amount = Convert.ToDecimal(row.Amount),
                    Source = source,
                    Status = "Unmatched",
                    TransactionType = typeVal
                };
            }).ToList();

            var filtered = await FilterDuplicateRowsAsync(records, source);
            return (filtered, null);
        }
        catch (Exception)
        {
            return (new List<Transaction>(), "Error parsing Excel. Please ensure headers are: Date, Description, Reference, Amount. Type is optional.");
        }
    }
}
