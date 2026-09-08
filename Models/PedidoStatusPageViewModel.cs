namespace sassClaude.Models;

public sealed record PedidoStatusPageViewModel(
    string Status, string Titulo, string Descricao, bool OrdenarAscendente, bool MostrarRecibo);
