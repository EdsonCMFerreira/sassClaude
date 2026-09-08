namespace sassClaude.Models;

public class PedidoItem
{
    public int Id { get; set; }

    public int PedidoId { get; set; }

    public Pedido? Pedido { get; set; }

    public int? ProdutoId { get; set; }

    public Produto? Produto { get; set; }

    public int Quantidade { get; set; } = 1;

    public decimal ValorUnitario { get; set; }
}
