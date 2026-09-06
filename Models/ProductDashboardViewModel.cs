namespace sassClaude.Models;

public sealed record ProductExpiryRow(string Codigo, string Descricao, DateTime Validade, int DiasRestantes);

public sealed record SupplierBreakdownRow(string Fornecedor, int Quantidade);

public sealed class ProductDashboardViewModel
{
    public int TotalProdutos { get; set; }
    public int Vencidos { get; set; }
    public int VenceEm30Dias { get; set; }
    public decimal LucroMedioPercentual { get; set; }
    public decimal ValorTotalCompra { get; set; }
    public decimal ValorTotalVenda { get; set; }
    public decimal LucroPotencialTotal { get; set; }
    public List<ProductExpiryRow> ProximosVencimentos { get; set; } = new();
    public List<SupplierBreakdownRow> TopFornecedores { get; set; } = new();
    public string? ProdutoMaisLucrativo { get; set; }
    public decimal? ProdutoMaisLucrativoPercentual { get; set; }
    public string? ProdutoMenosLucrativo { get; set; }
    public decimal? ProdutoMenosLucrativoPercentual { get; set; }
}
