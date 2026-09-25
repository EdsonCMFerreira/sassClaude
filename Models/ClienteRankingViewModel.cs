namespace Saas.Models;

public sealed record ClienteRankingRow(
    int ClienteId, string ClienteNome, decimal TotalComprado, int QuantidadePedidos,
    decimal TicketMedio, DateTime? UltimaCompraData);

public sealed class ClienteRankingViewModel
{
    public List<ClienteRankingRow> Clientes { get; set; } = new();

    public decimal TotalGeral { get; set; }

    public int TotalClientesCompradores { get; set; }
}
