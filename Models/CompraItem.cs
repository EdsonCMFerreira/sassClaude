using Saas.Data;

namespace Saas.Models;

public class CompraItem : ITenantScoped
{
    public int Id { get; set; }

    public int EmpresaId { get; set; }

    public int CompraId { get; set; }

    public Compra? Compra { get; set; }

    public int? ProdutoId { get; set; }

    public Produto? Produto { get; set; }

    public int Quantidade { get; set; } = 1;

    public decimal ValorUnitario { get; set; }
}
