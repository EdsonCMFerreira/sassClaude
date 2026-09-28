using System.ComponentModel.DataAnnotations;
using Saas.Data;

namespace Saas.Models;

public class Compra : ITenantScoped
{
    public int Id { get; set; }

    public int EmpresaId { get; set; }

    public int NumeroCompra { get; set; }

    public int? FornecedorId { get; set; }

    public Fornecedor? Fornecedor { get; set; }

    public List<CompraItem> Itens { get; set; } = new();

    public DateTime DataCompra { get; set; }

    public DateTime? DataVencimento { get; set; }

    public DateTime? DataPagamento { get; set; }

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
