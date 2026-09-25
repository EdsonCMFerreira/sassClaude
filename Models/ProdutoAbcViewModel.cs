namespace Saas.Models;

public sealed record ProdutoAbcRow(
    string Codigo, string Descricao, decimal Receita,
    decimal PercentualReceita, decimal PercentualAcumulado, string Classe);

public sealed class ProdutoAbcViewModel
{
    public List<ProdutoAbcRow> Produtos { get; set; } = new();
    public decimal ReceitaTotal { get; set; }

    public int ClasseACount { get; set; }
    public int ClasseBCount { get; set; }
    public int ClasseCCount { get; set; }

    public decimal ClasseAPercentualReceita { get; set; }
    public decimal ClasseBPercentualReceita { get; set; }
    public decimal ClasseCPercentualReceita { get; set; }
}
