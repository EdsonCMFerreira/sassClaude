using System.ComponentModel.DataAnnotations;

namespace sassClaude.Models;

public class Venda
{
    public int Id { get; set; }

    public int? ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    public List<VendaItem> Itens { get; set; } = new();

    public DateTime DataVenda { get; set; }

    [StringLength(50)]
    public string NumeroNota { get; set; } = string.Empty;

    [StringLength(30)]
    public string FormaPagamento { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Pendente";

    public decimal PercentualDesconto { get; set; }

    [StringLength(500)]
    public string Observacoes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
