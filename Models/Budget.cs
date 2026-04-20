using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFTRS.Models;

public class Budget
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string CategoryName { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyLimit { get; set; }

    public string Month { get; set; } = DateTime.Now.ToString("MMMM yyyy");
}