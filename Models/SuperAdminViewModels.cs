namespace sassClaude.Models;

public sealed record EmpresaSummaryRow(int Id, string Nome, string Plano, DateTime CreatedAt, int LoginCount, int ProdutoCount, int ClienteCount, int PedidoCount);

public sealed class SuperAdminViewModel
{
    public List<EmpresaSummaryRow> Empresas { get; set; } = new();
    public IReadOnlyList<string> AllowedPlanos { get; set; } = Array.Empty<string>();
}
