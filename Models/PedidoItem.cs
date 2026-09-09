using Saas.Data;

namespace Saas.Models;

public class PedidoItem : ITenantScoped
{
    public int Id { get; set; }

    public int EmpresaId { get; set; }

    public int PedidoId { get; set; }

    public Pedido? Pedido { get; set; }

    public int? ProdutoId { get; set; }

    public Produto? Produto { get; set; }

    public int Quantidade { get; set; } = 1;

    public decimal ValorUnitario { get; set; }
}
