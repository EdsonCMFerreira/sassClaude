namespace sassClaude.Models;

public sealed record DreRow(DateTime Mes, decimal Entradas);

public sealed class FinanceiroViewModel
{
    public decimal TotalEntradas { get; set; }
    public List<DreRow> Meses { get; set; } = new();
}
