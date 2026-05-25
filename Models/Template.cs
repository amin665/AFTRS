using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFTRS.Models;

public class Template
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TemplateID { get; set; }

    [Required]
    public int CategoryID { get; set; }

    public Category? Category { get; set; }

    [Required]
    [MaxLength(100)]
    public string DescriptionName { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(20)]
    public string Frequency { get; set; } = "Monthly"; // Monthly / Weekly

    [Required]
    public DateTime NextRunDate { get; set; }
}
