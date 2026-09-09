namespace Saas.Models;

public sealed record StatusBreakdownRow(string Status, int Quantidade);

public sealed record FaturamentoPontoRow(string Rotulo, decimal Total);

public sealed record ProdutoMaisPedidoRow(string ProdutoNome, int QuantidadeTotal);

public sealed class PedidoDashboardViewModel
{
    public int TotalPedidos { get; set; }

    public int PedidosPendentes { get; set; }

    public decimal ValorTotalConcluidos { get; set; }

    public decimal TicketMedio { get; set; }

    public List<StatusBreakdownRow> PorStatus { get; set; } = [];

    public List<FaturamentoPontoRow> Faturamento { get; set; } = [];

    public string FaturamentoTitulo { get; set; } = "Últimos 6 meses";

    public List<ProdutoMaisPedidoRow> ProdutosMaisPedidos { get; set; } = [];

    public int? MesSelecionado { get; set; }

    public int? AnoSelecionado { get; set; }

    public List<int> AnosDisponiveis { get; set; } = [];

    public string PeriodoDescricao { get; set; } = "no total";
}
