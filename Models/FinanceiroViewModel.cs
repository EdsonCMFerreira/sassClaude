namespace Saas.Models;

public sealed record DreRow(DateTime Mes, decimal Entradas, decimal Custo, decimal Lucro);

public sealed record ContaReceberRow(
    int PedidoId, int NumeroPedido, string ClienteNome, decimal Valor,
    DateTime? DataVencimento, int? DiasParaVencer, string Situacao);

public sealed record FormaPagamentoRow(string FormaPagamento, decimal Total, int Quantidade);

public sealed class FinanceiroViewModel
{
    public decimal TotalEntradas { get; set; }
    public List<DreRow> Meses { get; set; } = new();

    public decimal TotalEmAberto { get; set; }
    public decimal TotalVencido { get; set; }
    public decimal TotalVenceEm7Dias { get; set; }
    public int QuantidadeEmAberto { get; set; }
    public List<ContaReceberRow> ContasAReceber { get; set; } = new();

    public List<FormaPagamentoRow> VendasPorFormaPagamento { get; set; } = new();

    public decimal TotalCusto { get; set; }
    public decimal LucroBruto { get; set; }
    public decimal MargemBrutaPercentual { get; set; }
}
