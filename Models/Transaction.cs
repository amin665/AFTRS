using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFTRS.Models;

public class Transaction
{
    [Key]
    public int Id { get; set; }

    [Required]
    public DateTime TransactionDate { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    public string? ReferenceNumber { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")] // Requirement 3.5.1: Accuracy
    public decimal Amount { get; set; }

    [Required]
    public string Source { get; set; } // "Bank" or "Ledger"

    [Required]
    public string Status { get; set; } = "Unmatched"; // Matched, Unmatched, Resolved

    public DateTime UploadTimestamp { get; set; } = DateTime.Now;
    public int? MatchedTransactionId { get; set; }
    public string? Category { get; set; }

    /// <summary>Credit or Debit (SRS 3.2.2)</summary>
    public string? TransactionType { get; set; }

    [Required]
    public int BatchId { get; set; }

    [ForeignKey("BatchId")]
    public ReconciliationBatch? Batch { get; set; }
}