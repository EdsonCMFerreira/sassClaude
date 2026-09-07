namespace sassClaude.Models;

public class VendaItem
{
    public int Id { get; set; }

    public int VendaId { get; set; }

    public Venda? Venda { get; set; }

    public int? ProductId { get; set; }

    public Product? Product { get; set; }

    public int Quantidade { get; set; } = 1;

    public decimal ValorUnitario { get; set; }
}
