using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFTRS.Models;

public class UserPermission
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int UserPermissionID { get; set; }

    [Required]
    public int UserID { get; set; }

    public User? User { get; set; }

    [Required]
    [MaxLength(50)]
    public string Permission { get; set; } = string.Empty;
}
