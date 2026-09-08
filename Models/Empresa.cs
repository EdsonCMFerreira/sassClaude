using System.ComponentModel.DataAnnotations;

namespace sassClaude.Models;

public class Empresa
{
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Plano { get; set; } = "Starter";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
