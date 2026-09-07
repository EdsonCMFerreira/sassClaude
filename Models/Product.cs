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

    public decimal ValorCompra { get; set; }

    public decimal ValorVenda { get; set; }

    public int? FornecedorId { get; set; }

    public Fornecedor? Fornecedor { get; set; }

    public int Quantidade { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
