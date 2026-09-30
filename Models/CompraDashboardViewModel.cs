namespace Saas.Models;

public sealed record FornecedorGastoRow(string FornecedorNome, decimal Total, int Quantidade);

public sealed record ProdutoMaisCompradoRow(string ProdutoNome, int QuantidadeTotal);

public sealed record GastoMensalRow(string Rotulo, decimal Total);

public sealed class CompraDashboardViewModel
{
    public int TotalCompras { get; set; }

    public decimal GastoTotal { get; set; }

    public decimal TotalEmAberto { get; set; }

    public decimal TotalPago { get; set; }

    public List<StatusBreakdownRow> PorStatus { get; set; } = [];

    public List<GastoMensalRow> GastoMensal { get; set; } = [];

    public List<FornecedorGastoRow> TopFornecedores { get; set; } = [];

    public List<ProdutoMaisCompradoRow> ProdutosMaisComprados { get; set; } = [];
}
