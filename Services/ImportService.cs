using System.Globalization;
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

    // New validation method to check for duplicates across the database
    private async Task<string?> ValidateDuplicatesAsync(List<Transaction> newTransactions, string source)
    {
        // Get all existing reference numbers for this source to compare
        var existingRefs = await _context.Transactions
            .Where(t => t.Source == source)
            .Select(t => t.ReferenceNumber)
            .Where(r => r != null)
            .ToListAsync();

        foreach (var t in newTransactions)
        {
            if (!string.IsNullOrEmpty(t.ReferenceNumber) && existingRefs.Contains(t.ReferenceNumber))
            {
                return $"Duplicate detected: Reference Number '{t.ReferenceNumber}' already exists in the {source} records. Upload aborted to prevent data duplication.";
            }
        }
        return null; // No duplicates found
    }

    public async Task<(List<Transaction> Data, string? Error)> ParseCsvWithValidation(Stream fileStream, string source)
    {
        using var reader = new StreamReader(fileStream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        
        try 
        {
            var records = csv.GetRecords<dynamic>().Select(row => new Transaction
            {
                TransactionDate = DateTime.Parse(row.Date),
                Description = row.Description,
                ReferenceNumber = row.Reference,
                Amount = decimal.Parse(row.Amount),
                Source = source,
                Status = "Unmatched"
            }).ToList();

            var error = await ValidateDuplicatesAsync(records, source);
            return error != null ? (new List<Transaction>(), error) : (records, null);
        }
        catch (Exception)
        {
            return (new List<Transaction>(), "Error parsing CSV. Please ensure columns are: Date, Description, Reference, Amount.");
        }
    }

    public async Task<(List<Transaction> Data, string? Error)> ParseExcelWithValidation(Stream fileStream, string source)
    {
        try
        {
            var rows = fileStream.Query(useHeaderRow: true).ToList();
            var records = rows.Select(row => new Transaction
            {
                TransactionDate = Convert.ToDateTime(row.Date),
                Description = Convert.ToString(row.Description),
                ReferenceNumber = Convert.ToString(row.Reference),
                Amount = Convert.ToDecimal(row.Amount),
                Source = source,
                Status = "Unmatched"
            }).ToList();

            var error = await ValidateDuplicatesAsync(records, source);
            return error != null ? (new List<Transaction>(), error) : (records, null);
        }
        catch (Exception)
        {
            return (new List<Transaction>(), "Error parsing Excel. Please ensure headers are: Date, Description, Reference, Amount.");
        }
    }
}