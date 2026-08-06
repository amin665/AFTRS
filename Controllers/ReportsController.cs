using System.Globalization;
using AFTRS.Data;
using AFTRS.Infrastructure;
using AFTRS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AFTRS.Controllers;

[RoleAuthorize("Manager", "Admin")]
[PermissionAuthorize(AppPermissions.Reports)]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ReconciliationSessionContext _sessions;

    public ReportsController(ApplicationDbContext context, ReconciliationSessionContext sessions)
    {
        _context = context;
        _sessions = sessions;
    }

    [HttpGet]
    public async Task<IActionResult> ReconciliationPdf()
    {
        var session = await _sessions.GetSelectedAsync();
        var language = UiText.Language(Request);
        var tx = await _context.Transactions
            .Where(t => t.SessionID == session.SessionID)
            .OrderByDescending(t => t.TransactionDate)
            .Take(2000)
            .ToListAsync();

        var manualJustifications = await GetManualJustificationsAsync(session.SessionID, tx);

        var total = await _context.Transactions.CountAsync(t => t.SessionID == session.SessionID);
        var reconciled = await _context.Transactions.CountAsync(t => t.SessionID == session.SessionID && t.Status == "Reconciled");
        var discrepancies = await _context.Transactions.CountAsync(t => t.SessionID == session.SessionID && t.Status == "Discrepancy");
        var manual = await _context.Transactions.CountAsync(t => t.SessionID == session.SessionID && t.Status == "Reconciled" && t.MatchMethod == "Manual");

        var now = DateTime.Now;
        static IContainer HeaderCell(IContainer container) => container
            .Border(0.75f)
            .BorderColor(Colors.Grey.Darken1)
            .Background(Colors.Grey.Lighten3)
            .Padding(4);

        static IContainer BodyCell(IContainer container) => container
            .Border(0.5f)
            .BorderColor(Colors.Grey.Lighten1)
            .Padding(4);

        var matchedPairGroups = tx
            .Where(t => t.Status == "Reconciled" && (t.MatchMethod == "Auto" || t.MatchMethod == "Manual") && t.MatchedTransactionID != null)
            .GroupBy(t => PairKey(t))
            .Where(g => g.Count() > 1)
            .OrderBy(g => g.First().MatchMethod == "Auto" ? 0 : 1)
            .ThenByDescending(g => g.Max(t => t.TransactionDate))
            .ToList();
        var groupedTransactionIds = matchedPairGroups.SelectMany(g => g.Select(t => t.TransactionID)).ToHashSet();
        var remainingRows = tx.Where(t => !groupedTransactionIds.Contains(t.TransactionID)).ToList();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI"));

                page.Header().Column(col =>
                {
                    col.Item().Text(UiText.T(language, "ReconciliationStatusReport")).FontSize(16).SemiBold();
                    col.Item().Text($"{UiText.T(language, "Generated")}: {now.ToString("g", CultureInfo.InvariantCulture)}").FontColor(Colors.Grey.Darken2);
                    col.Item().Text($"{UiText.T(language, "TotalTransactions")}: {total} | {UiText.T(language, "Reconciled")}: {reconciled} | {UiText.T(language, "Manual")}: {manual} | {UiText.T(language, "Discrepancies")}: {discrepancies}").FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(10).Column(content =>
                {
                    foreach (var group in matchedPairGroups)
                    {
                        var method = group.First().MatchMethod ?? "Manual";
                        var borderColor = method == "Auto" ? Colors.Green.Darken2 : Colors.Amber.Darken3;
                        var orderedGroup = group.OrderBy(t => t.Source == "Ledger" ? 0 : 1).ThenBy(t => t.TransactionID).ToList();

                        content.Item().PaddingBottom(6).Border(1.5f).BorderColor(borderColor).Column(groupContent =>
                        {
                            groupContent.Item().Background(method == "Auto" ? Colors.Green.Lighten5 : Colors.Amber.Lighten5).Padding(4)
                                .Text($"{UiText.T(language, method == "Auto" ? "AutoMatch" : "ManualMatch")}: {string.Join(" <-> ", orderedGroup.Select(t => $"#{t.TransactionID}"))}").SemiBold();

                            groupContent.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(65);
                                    cols.RelativeColumn(2.4f);
                                    cols.ConstantColumn(65);
                                    cols.ConstantColumn(55);
                                    cols.ConstantColumn(65);
                                    cols.ConstantColumn(55);
                                    cols.RelativeColumn(2.6f);
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Element(HeaderCell).Text(UiText.T(language, "Date")).SemiBold();
                                    h.Cell().Element(HeaderCell).Text(UiText.T(language, "Description")).SemiBold();
                                    h.Cell().Element(HeaderCell).AlignRight().Text(UiText.T(language, "Amount")).SemiBold();
                                    h.Cell().Element(HeaderCell).Text(UiText.T(language, "Source")).SemiBold();
                                    h.Cell().Element(HeaderCell).Text(UiText.T(language, "Status")).SemiBold();
                                    h.Cell().Element(HeaderCell).Text(UiText.T(language, "Method")).SemiBold();
                                    h.Cell().Element(HeaderCell).Text(UiText.T(language, "NotesJustification")).SemiBold();
                                });

                                foreach (var t in orderedGroup)
                                {
                                    var notes = GetReportNotes(t, manualJustifications);
                                    table.Cell().Element(BodyCell).Text(t.TransactionDate.ToString("dd/MM/yyyy"));
                                    table.Cell().Element(BodyCell).Text(t.Description);
                                    table.Cell().Element(BodyCell).AlignRight().Text(t.Amount.ToString("N2", CultureInfo.InvariantCulture));
                                    table.Cell().Element(BodyCell).Text(LocalizeSource(language, t.Source));
                                    table.Cell().Element(BodyCell).Text(LocalizeStatus(language, t.Status));
                                    table.Cell().Element(BodyCell).Text(LocalizeMatchMethod(language, t.MatchMethod));
                                    table.Cell().Element(BodyCell).Text(notes);
                                }
                            });
                        });
                    }

                    if (remainingRows.Count > 0)
                    {
                        content.Item().PaddingTop(4).Text(UiText.T(language, "DiscrepanciesUngrouped")).SemiBold();
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(65);
                                cols.RelativeColumn(2.4f);
                                cols.ConstantColumn(65);
                                cols.ConstantColumn(55);
                                cols.ConstantColumn(65);
                                cols.ConstantColumn(55);
                                cols.RelativeColumn(2.6f);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text(UiText.T(language, "Date")).SemiBold();
                                h.Cell().Element(HeaderCell).Text(UiText.T(language, "Description")).SemiBold();
                                h.Cell().Element(HeaderCell).AlignRight().Text(UiText.T(language, "Amount")).SemiBold();
                                h.Cell().Element(HeaderCell).Text(UiText.T(language, "Source")).SemiBold();
                                h.Cell().Element(HeaderCell).Text(UiText.T(language, "Status")).SemiBold();
                                h.Cell().Element(HeaderCell).Text(UiText.T(language, "Method")).SemiBold();
                                h.Cell().Element(HeaderCell).Text(UiText.T(language, "NotesJustification")).SemiBold();
                            });

                            foreach (var t in remainingRows)
                            {
                                var notes = GetReportNotes(t, manualJustifications);
                                table.Cell().Element(BodyCell).Text(t.TransactionDate.ToString("dd/MM/yyyy"));
                                table.Cell().Element(BodyCell).Text(t.Description);
                                table.Cell().Element(BodyCell).AlignRight().Text(t.Amount.ToString("N2", CultureInfo.InvariantCulture));
                                table.Cell().Element(BodyCell).Text(LocalizeSource(language, t.Source));
                                table.Cell().Element(BodyCell).Text(LocalizeStatus(language, t.Status));
                                table.Cell().Element(BodyCell).Text(LocalizeMatchMethod(language, t.MatchMethod));
                                table.Cell().Element(BodyCell).Text(notes);
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.DefaultTextStyle(s => s.FontColor(Colors.Grey.Darken2));
                    x.Span("AFTRS ");
                    x.Span("|");
                    x.Span($" {UiText.T(language, "Page")} ");
                    x.CurrentPageNumber();
                    x.Span($" {UiText.T(language, "Of")} ");
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
        var session = await _sessions.GetSelectedAsync();
        var language = UiText.Language(Request);
        var tx = await _context.Transactions
            .Where(t => t.SessionID == session.SessionID)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        var manualJustifications = await GetManualJustificationsAsync(session.SessionID, tx);

        var rows = tx.Select(t => new Dictionary<string, object?>
        {
            [UiText.T(language, "Date")] = t.TransactionDate.ToString("dd/MM/yyyy"),
            [UiText.T(language, "Description")] = t.Description,
            ["ReferenceNumber"] = t.ReferenceNumber,
            [UiText.T(language, "Amount")] = t.Amount,
            [UiText.T(language, "Source")] = LocalizeSource(language, t.Source),
            [UiText.T(language, "Status")] = LocalizeStatus(language, t.Status),
            [UiText.T(language, "Method")] = LocalizeMatchMethod(language, t.MatchMethod),
            ["MatchGroup"] = GetMatchGroup(language, t),
            [UiText.T(language, "NotesJustification")] = GetReportNotes(t, manualJustifications)
        }).ToList();

        using var ms = new MemoryStream();
        ms.SaveAs(rows);
        ms.Position = 0;
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"AFTRS-Reconciliation-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    private static string PairKey(Models.Transaction transaction)
    {
        var matchedId = transaction.MatchedTransactionID!.Value;
        var first = Math.Min(transaction.TransactionID, matchedId);
        var second = Math.Max(transaction.TransactionID, matchedId);
        return $"{first}-{second}";
    }

    private static string GetMatchGroup(string language, Models.Transaction transaction)
    {
        if (transaction.Status == "Reconciled" && transaction.MatchedTransactionID != null)
            return $"{LocalizeMatchMethod(language, transaction.MatchMethod)} {PairKey(transaction)}";

        return string.Empty;
    }

    private static string LocalizeSource(string language, string source) => source switch
    {
        "Ledger" => UiText.T(language, "Ledger"),
        "Bank" => UiText.T(language, "Bank"),
        _ => source
    };

    private static string LocalizeStatus(string language, string status) => status switch
    {
        "Reconciled" => UiText.T(language, "Reconciled"),
        "Discrepancy" => UiText.T(language, "Discrepancy"),
        _ => status
    };

    private static string LocalizeMatchMethod(string language, string? method) => method switch
    {
        "Auto" => UiText.T(language, "AutoReconciled"),
        "Manual" => UiText.T(language, "Manual"),
        null => "—",
        _ => method
    };

    private async Task<Dictionary<int, string>> GetManualJustificationsAsync(int sessionId, List<Models.Transaction> transactions)
    {
        var transactionIds = transactions
            .Where(t => t.Status == "Reconciled" && t.MatchMethod == "Manual")
            .Select(t => t.TransactionID)
            .Concat(transactions.Where(t => t.Status == "Reconciled" && t.MatchMethod == "Manual" && t.MatchedTransactionID != null).Select(t => t.MatchedTransactionID!.Value))
            .Distinct()
            .ToList();

        var auditLogs = await _context.FinancialAuditLogs
            .Where(a => a.SessionID == sessionId && transactionIds.Contains(a.TransactionID))
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();

        var result = new Dictionary<int, string>();
        foreach (var log in auditLogs)
        {
            result.TryAdd(log.TransactionID, log.Justification);
            foreach (var transaction in transactions.Where(t => t.MatchedTransactionID == log.TransactionID || t.TransactionID == log.TransactionID))
            {
                result.TryAdd(transaction.TransactionID, log.Justification);
                if (transaction.MatchedTransactionID != null)
                    result.TryAdd(transaction.MatchedTransactionID.Value, log.Justification);
            }
        }

        return result;
    }

    private static string GetReportNotes(Models.Transaction transaction, Dictionary<int, string> manualJustifications)
    {
        if (transaction.Status == "Reconciled" && transaction.MatchMethod == "Manual")
        {
            if (manualJustifications.TryGetValue(transaction.TransactionID, out var justification))
                return justification;
            if (transaction.MatchedTransactionID != null && manualJustifications.TryGetValue(transaction.MatchedTransactionID.Value, out justification))
                return justification;
        }

        if (transaction.Status == "Discrepancy" && !string.IsNullOrWhiteSpace(transaction.DiscrepancyComment))
            return transaction.DiscrepancyComment;

        return "—";
    }

    [HttpGet]
    public async Task<IActionResult> BudgetPdf()
    {
        var session = await _sessions.GetSelectedAsync();
        var language = UiText.Language(Request);
        var now = DateTime.Today;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var budgets = await _context.BudgetTargets
            .Include(b => b.Category)
            .Where(b => b.SessionID == session.SessionID && b.TargetMonth == now.Month && b.TargetYear == now.Year)
            .OrderBy(b => b.Category!.Name)
            .ToListAsync();

        var actuals = await _context.Transactions
            .Where(t => t.SessionID == session.SessionID && t.Source == "Ledger" && t.CategoryID != null && t.TransactionDate >= monthStart && t.TransactionDate < monthEnd)
            .GroupBy(t => t.CategoryID)
            .Select(g => new { CategoryID = g.Key!.Value, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CategoryID, x => x.Total);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));

                page.Header().Column(col =>
                {
                    col.Item().Text(UiText.T(language, "BudgetVsActualReport")).FontSize(16).SemiBold();
                    col.Item().Text($"{UiText.T(language, "Month")}: {now.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}").FontColor(Colors.Grey.Darken2);
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
                        h.Cell().Text(UiText.T(language, "Category")).SemiBold();
                        h.Cell().AlignRight().Text(UiText.T(language, "Budget")).SemiBold();
                        h.Cell().AlignRight().Text(UiText.T(language, "Actual")).SemiBold();
                        h.Cell().AlignRight().Text(UiText.T(language, "Variance")).SemiBold();
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
