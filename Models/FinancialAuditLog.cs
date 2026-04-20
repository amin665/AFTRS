using System.ComponentModel.DataAnnotations;

namespace AFTRS.Models;

public class FinancialAuditLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Action { get; set; } = "Manual Match";
    [Required]
    public string BatchName { get; set; } = string.Empty;
    [Required]
    public int LedgerTransactionId { get; set; }

    [Required]
    public int BankTransactionId { get; set; }

    [Required]
    public string UserEmail { get; set; } // Who did it?

    [Required]
    public string Justification { get; set; } // Why? (Requirement FR-16)

    public DateTime Timestamp { get; set; } = DateTime.Now;
}