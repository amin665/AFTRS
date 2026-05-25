using System.Globalization;
using System.Security.Cryptography;
using AFTRS.Data;
using AFTRS.Models;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;

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
    /// SRS Validation 1 (Duplicate File): checks SHA-256 hash exists in DB.
    /// </summary>
    public async Task<string?> CheckFileHashAsync(string fileName, string fileHash)
    {
        var exists = await _context.FileUploadRecords.AnyAsync(r => r.FileHash == fileHash);
        if (exists)
            return $"Duplicate file detected: '{fileName}' (or a file with identical content) has already been uploaded. Upload aborted to prevent data duplication.";
        return null;
    }

    /// <summary>Records a file upload entry after successful import.</summary>
    public void RecordFileUpload(string fileName, string fileHash, string source)
    {
        _context.FileUploadRecords.Add(new FileUploadRecord
        {
            FileName = fileName,
            FileHash = fileHash,
            Source = source,
            UploadedAt = DateTime.Now
        });
    }

    /// <summary>
    /// SRS Validation 2 (Duplicate Row): skip any row where Date + Ref + Amount matches an existing record.
    /// </summary>
    private async Task<List<Transaction>> FilterDuplicateRowsAsync(List<Transaction> newTransactions)
    {
        var existing = await _context.Transactions
            .Select(t => new { t.ReferenceNumber, t.TransactionDate, t.Amount })
            .ToListAsync();

        var existingSet = existing
            .Select(e => $"{e.ReferenceNumber}|{e.TransactionDate:yyyy-MM-dd}|{e.Amount}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return newTransactions
            .Where(t =>
            {
                var key = $"{t.ReferenceNumber}|{t.TransactionDate:yyyy-MM-dd}|{t.Amount}";
                return !existingSet.Contains(key);
            })
            .ToList();
    }

    private static bool TryParseDateDmy(string? value, out DateTime date)
    {
        return DateTime.TryParseExact(
            value?.Trim(),
            "dd/MM/yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    private static string? GetString(IDictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v == null) return null;
        return Convert.ToString(v);
    }

    private static decimal? GetDecimal(IDictionary<string, object> dict, string key)
    {
        var s = GetString(dict, key);
        if (s == null) return null;
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
        // Also try current culture as a fallback for Excel exports.
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.CurrentCulture, out d)) return d;
        return null;
    }

    /// <summary>
    /// SRS Validation 3 (Double-Entry Balance): if ledger totals are unbalanced, warn.
    /// Assumes an optional "Type" column: Credit/Debit.
    /// </summary>
    private static string? ValidateLedgerBalance(IEnumerable<(decimal Amount, string? Type)> ledgerRows)
    {
        decimal sum = 0m;

        foreach (var r in ledgerRows)
        {
            if (string.IsNullOrWhiteSpace(r.Type))
                continue; // can't validate without debit/credit

            var type = r.Type.Trim();
            if (type.Equals("Debit", StringComparison.OrdinalIgnoreCase))
                sum -= r.Amount;
            else if (type.Equals("Credit", StringComparison.OrdinalIgnoreCase))
                sum += r.Amount;
        }

        if (sum != 0m)
            return $"Ledger file appears UNBALANCED (debits/credits total is {sum:N2} LYD, expected 0.00). Upload accepted but flagged for review.";

        return null;
    }

    public async Task<(List<Transaction> Data, string? Error)> ParseCsvWithValidation(Stream fileStream, string source)
    {
        using var reader = new StreamReader(fileStream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        try
        {
            var rows = csv.GetRecords<dynamic>().ToList();
            var records = new List<Transaction>();
            var ledgerForBalance = new List<(decimal Amount, string? Type)>();

            foreach (var row in rows)
            {
                var dict = (IDictionary<string, object>)row;
                var dateRaw = GetString(dict, "Date");
                var desc = GetString(dict, "Description");
                var refNum = GetString(dict, "Reference Number") ?? GetString(dict, "ReferenceNumber") ?? GetString(dict, "Reference") ?? GetString(dict, "Ref");
                var amount = GetDecimal(dict, "Amount");

                if (!TryParseDateDmy(dateRaw, out var date) || string.IsNullOrWhiteSpace(desc) || amount == null || string.IsNullOrWhiteSpace(refNum))
                    return (new List<Transaction>(), "Invalid file format. Required columns: Date (DD/MM/YYYY), Description, Reference Number, Amount.");

                var t = new Transaction
                {
                    TransactionDate = date,
                    Description = desc!,
                    ReferenceNumber = refNum,
                    Amount = decimal.Round(amount.Value, 2, MidpointRounding.AwayFromZero),
                    Source = source,
                    Status = "Unmatched"
                };

                records.Add(t);

                var type = GetString(dict, "Type");
                if (source == "Ledger")
                    ledgerForBalance.Add((t.Amount, type));
            }

            var filtered = await FilterDuplicateRowsAsync(records);
            var warning = source == "Ledger" ? ValidateLedgerBalance(ledgerForBalance) : null;
            return (filtered, warning);
        }
        catch (Exception)
        {
            return (new List<Transaction>(), "Error parsing CSV. Please ensure headers include: Date (DD/MM/YYYY), Description, Reference Number, Amount.");
        }
    }

    public async Task<(List<Transaction> Data, string? Error)> ParseExcelWithValidation(Stream fileStream, string source)
    {
        try
        {
            var rows = fileStream.Query(useHeaderRow: true).ToList();
            var records = new List<Transaction>();
            var ledgerForBalance = new List<(decimal Amount, string? Type)>();

            foreach (var row in rows)
            {
                var dict = (IDictionary<string, object>)row;
                var dateRaw = GetString(dict, "Date");
                var desc = GetString(dict, "Description");
                var refNum = GetString(dict, "Reference Number") ?? GetString(dict, "ReferenceNumber") ?? GetString(dict, "Reference") ?? GetString(dict, "Ref");
                var amount = GetDecimal(dict, "Amount");

                if (!TryParseDateDmy(dateRaw, out var date) || string.IsNullOrWhiteSpace(desc) || amount == null || string.IsNullOrWhiteSpace(refNum))
                    return (new List<Transaction>(), "Invalid file format. Required columns: Date (DD/MM/YYYY), Description, Reference Number, Amount.");

                var t = new Transaction
                {
                    TransactionDate = date,
                    Description = desc!,
                    ReferenceNumber = refNum,
                    Amount = decimal.Round(amount.Value, 2, MidpointRounding.AwayFromZero),
                    Source = source,
                    Status = "Unmatched"
                };

                records.Add(t);

                var type = GetString(dict, "Type");
                if (source == "Ledger")
                    ledgerForBalance.Add((t.Amount, type));
            }

            var filtered = await FilterDuplicateRowsAsync(records);
            var warning = source == "Ledger" ? ValidateLedgerBalance(ledgerForBalance) : null;
            return (filtered, warning);
        }
        catch (Exception)
        {
            return (new List<Transaction>(), "Error parsing Excel. Please ensure headers include: Date (DD/MM/YYYY), Description, Reference Number, Amount.");
        }
    }
}
