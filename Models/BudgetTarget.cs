using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFTRS.Models;

public class BudgetTarget
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int BudgetID { get; set; }

    public int SessionID { get; set; }
    public ReconciliationSession? Session { get; set; }

    [Required]
    public int CategoryID { get; set; }

    public Category? Category { get; set; }

    [Required]
    public int TargetMonth { get; set; }

    [Required]
    public int TargetYear { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TargetAmount { get; set; }
}
