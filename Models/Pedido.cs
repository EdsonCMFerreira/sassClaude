using System.ComponentModel.DataAnnotations;
using sassClaude.Data;

namespace sassClaude.Models;

public class Pedido : ITenantScoped
{
    public int Id { get; set; }

    public int EmpresaId { get; set; }

    public int NumeroPedido { get; set; }

    public int? ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    public List<PedidoItem> Itens { get; set; } = new();

    public DateTime DataPedido { get; set; }

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
