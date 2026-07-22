using System.Globalization;
using AFTRS.Data;
using AFTRS.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AFTRS.Controllers;

[RoleAuthorize("Manager", "Admin")]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReportsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> ReconciliationPdf()
    {
        var tx = await _context.Transactions
            .OrderByDescending(t => t.TransactionDate)
            .Take(2000)
            .ToListAsync();

        var total = await _context.Transactions.CountAsync();
        var reconciled = await _context.Transactions.CountAsync(t => t.Status == "Reconciled");
        var discrepancies = await _context.Transactions.CountAsync(t => t.Status == "Discrepancy");
        var manual = await _context.Transactions.CountAsync(t => t.Status == "Reconciled" && t.MatchMethod == "Manual");

        var now = DateTime.Now;
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("AFTRS Reconciliation Status Report").FontSize(16).SemiBold();
                    col.Item().Text($"Generated: {now.ToString("g", CultureInfo.InvariantCulture)}").FontColor(Colors.Grey.Darken2);
                    col.Item().Text($"Total: {total} | Reconciled: {reconciled} | Manual: {manual} | Discrepancies: {discrepancies}").FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(70);
                        cols.RelativeColumn(3);
                        cols.ConstantColumn(70);
                        cols.ConstantColumn(55);
                        cols.ConstantColumn(60);
                        cols.ConstantColumn(60);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Text("Date").SemiBold();
                        h.Cell().Text("Description").SemiBold();
                        h.Cell().Text("Amount").SemiBold();
                        h.Cell().Text("Source").SemiBold();
                        h.Cell().Text("Status").SemiBold();
                        h.Cell().Text("Method").SemiBold();
                        h.Cell().ColumnSpan(6).PaddingVertical(5).LineHorizontal(1);
                    });

                    foreach (var t in tx)
                    {
                        table.Cell().Text(t.TransactionDate.ToString("dd/MM/yyyy"));
                        table.Cell().Text(t.Description);
                        table.Cell().AlignRight().Text(t.Amount.ToString("N2", CultureInfo.InvariantCulture));
                        table.Cell().Text(t.Source);
                        table.Cell().Text(t.Status);
                        table.Cell().Text(t.MatchMethod ?? "—");
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.DefaultTextStyle(s => s.FontColor(Colors.Grey.Darken2));
                    x.Span("AFTRS ");
                    x.Span("|");
                    x.Span(" Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        var bytes = doc.GeneratePdf();
        return File(bytes, "application/pdf", $"AFTRS-Reconciliation-{now:yyyyMMdd-HHmm}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ReconciliationExcel()
    {
        var tx = await _context.Transactions
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        var rows = tx.Select(t => new
        {
            TransactionDate = t.TransactionDate.ToString("dd/MM/yyyy"),
            t.Description,
            t.ReferenceNumber,
            Amount = t.Amount,
            t.Source,
            t.Status,
            MatchMethod = t.MatchMethod
        }).ToList();

        using var ms = new MemoryStream();
        ms.SaveAs(rows);
        ms.Position = 0;
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"AFTRS-Reconciliation-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> BudgetPdf()
    {
        var now = DateTime.Today;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var budgets = await _context.BudgetTargets
            .Include(b => b.Category)
            .Where(b => b.TargetMonth == now.Month && b.TargetYear == now.Year)
            .OrderBy(b => b.Category!.Name)
            .ToListAsync();

        var actuals = await _context.Transactions
            .Where(t => t.Source == "Ledger" && t.CategoryID != null && t.TransactionDate >= monthStart && t.TransactionDate < monthEnd)
            .GroupBy(t => t.CategoryID)
            .Select(g => new { CategoryID = g.Key!.Value, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CategoryID, x => x.Total);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("AFTRS Budget vs Actual Report").FontSize(16).SemiBold();
                    col.Item().Text($"Month: {now:MMMM yyyy}").FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.ConstantColumn(80);
                        cols.ConstantColumn(80);
                        cols.ConstantColumn(70);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Text("Category").SemiBold();
                        h.Cell().AlignRight().Text("Budget").SemiBold();
                        h.Cell().AlignRight().Text("Actual").SemiBold();
                        h.Cell().AlignRight().Text("Variance").SemiBold();
                        h.Cell().ColumnSpan(4).PaddingVertical(5).LineHorizontal(1);
                    });

                    foreach (var b in budgets)
                    {
                        var spent = actuals.TryGetValue(b.CategoryID, out var a) ? a : 0m;
                        var variance = spent - b.TargetAmount;
                        table.Cell().Text(b.Category?.Name ?? "—");
                        table.Cell().AlignRight().Text(b.TargetAmount.ToString("N2", CultureInfo.InvariantCulture));
                        table.Cell().AlignRight().Text(spent.ToString("N2", CultureInfo.InvariantCulture));
                        table.Cell().AlignRight().Text(variance.ToString("N2", CultureInfo.InvariantCulture));
                    }
                });
            });
        });

        var bytes = doc.GeneratePdf();
        return File(bytes, "application/pdf", $"AFTRS-Budget-{now:yyyyMM}.pdf");
    }
}
