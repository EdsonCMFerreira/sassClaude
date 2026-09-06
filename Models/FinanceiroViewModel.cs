namespace sassClaude.Models;

public sealed record DreRow(DateTime Mes, decimal Entradas, decimal Saidas, decimal Resultado);

public sealed class FinanceiroViewModel
{
    public decimal TotalEntradas { get; set; }
    public decimal TotalSaidas { get; set; }
    public decimal ResultadoTotal { get; set; }
    public List<DreRow> Meses { get; set; } = new();
}
