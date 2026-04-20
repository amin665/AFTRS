using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AFTRS.Models;

namespace AFTRS.Services;

public class ReportService
{
    public byte[] GenerateBatchPdf(ReconciliationBatch batch)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.Header().Text($"AFTRS Reconciliation Report: {batch.Name}").FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);

                page.Content().Column(col =>
                {
                    col.Item().Text($"Date Generated: {DateTime.Now:f}");
                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Date");
                            header.Cell().Text("Description");
                            header.Cell().Text("Amount");
                            header.Cell().Text("Status");
                        });

                        foreach (var t in batch.Transactions)
                        {
                            table.Cell().Text(t.TransactionDate.ToShortDateString());
                            table.Cell().Text(t.Description);
                            table.Cell().Text($"{t.Amount} LYD");
                            table.Cell().Text(t.Status);
                        }
                    });
                });
            });
        });

        return document.GeneratePdf();
    }
}