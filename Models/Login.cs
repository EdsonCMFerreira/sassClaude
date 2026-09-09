using System.ComponentModel.DataAnnotations;
using Saas.Data;

namespace Saas.Models;

public class Login : ITenantScoped
{
    public int Id { get; set; }

    public int EmpresaId { get; set; }

    [Required]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool EmailConfirmed { get; set; }

    public bool IsSuperAdmin { get; set; }

    [Required]
    [StringLength(20)]
    public string Role { get; set; } = "Colaborador";
}
