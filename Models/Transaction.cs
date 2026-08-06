using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFTRS.Models;

public class Transaction
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TransactionID { get; set; }

    public int SessionID { get; set; }
    public ReconciliationSession? Session { get; set; }

    public int? CategoryID { get; set; }
    public Category? Category { get; set; }

    public int? MatchedTransactionID { get; set; }
    public Transaction? MatchedTransaction { get; set; }

    [Required]
    public DateTime TransactionDate { get; set; }

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    [Required]
    [MaxLength(20)]
    public string Source { get; set; } = string.Empty; // Ledger / Bank

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Discrepancy"; // Discrepancy / Reconciled

    // Used for UI color-coding per SRS: Auto (green) vs Manual (yellow) when Reconciled.
    [MaxLength(20)]
    public string? MatchMethod { get; set; } // Auto / Manual

    [MaxLength(1000)]
    public string? DiscrepancyComment { get; set; }
}
