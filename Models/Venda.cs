using System.ComponentModel.DataAnnotations;

namespace sassClaude.Models;

public class Venda
{
    public int Id { get; set; }

    public int? ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    public int? ProductId { get; set; }

    public Product? Product { get; set; }

    public int Quantidade { get; set; } = 1;

    public decimal ValorUnitario { get; set; }

    public DateTime DataVenda { get; set; }

    [StringLength(50)]
    public string NumeroNota { get; set; } = string.Empty;

    [StringLength(30)]
    public string FormaPagamento { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Pendente";

    [StringLength(500)]
    public string Observacoes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
