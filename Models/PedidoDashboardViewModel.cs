namespace sassClaude.Models;

public sealed record StatusBreakdownRow(string Status, int Quantidade);

public sealed record FaturamentoMensalRow(DateTime Mes, decimal Total);

public sealed record ProdutoMaisPedidoRow(string ProdutoNome, int QuantidadeTotal);

public sealed class PedidoDashboardViewModel
{
    public int TotalPedidos { get; set; }

    public int PedidosPendentes { get; set; }

    public decimal ValorTotalConcluidos { get; set; }

    public decimal TicketMedio { get; set; }

    public List<StatusBreakdownRow> PorStatus { get; set; } = [];

    public List<FaturamentoMensalRow> FaturamentoMensal { get; set; } = [];

    public List<ProdutoMaisPedidoRow> ProdutosMaisPedidos { get; set; } = [];
}
