using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFTRS.Models;

public class FinancialAuditLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int AuditID { get; set; }

    [Required]
    public int TransactionID { get; set; }

    public Transaction? Transaction { get; set; }

    [Required]
    public int UserID { get; set; }

    public User? User { get; set; }

    [Required]
    [MaxLength(20)]
    public string OldStatus { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string NewStatus { get; set; } = string.Empty;

    [Required]
    public string Justification { get; set; } = string.Empty;

    [Required]
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
