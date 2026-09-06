namespace sassClaude.Models;

public sealed record EstoqueRow(
    string Codigo, string Descricao, string FornecedorNome,
    int QuantidadeComprada, int QuantidadeVendida, int SaldoEstoque, decimal ValorEstoque);

public sealed class EstoqueViewModel
{
    public List<EstoqueRow> Itens { get; set; } = new();
    public decimal ValorTotalEstoque { get; set; }
    public int ProdutosComSaldoNegativo { get; set; }
    public int ProdutosSemMovimentacao { get; set; }
}
