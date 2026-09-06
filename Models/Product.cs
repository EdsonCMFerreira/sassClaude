using System.ComponentModel.DataAnnotations;

namespace sassClaude.Models;

public class Product
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Codigo { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Descricao { get; set; } = string.Empty;

    public DateTime Validade { get; set; }

    public decimal Valor { get; set; }

    [Required]
    [StringLength(150)]
    public string Fornecedor { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
