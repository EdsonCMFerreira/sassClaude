using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Data;

namespace Saas.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly SassDbContext _context;

    public DashboardController(SassDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var totalUsers = await _context.Logins.CountAsync();
        var hoje = DateTime.UtcNow.Date;
        var alertas = new List<AlertaItem>();

        var produtos = await _context.Produtos.ToListAsync();

        var produtosVencidos = produtos.Count(p => p.Validade.Date < hoje);
        if (produtosVencidos > 0)
        {
            alertas.Add(new AlertaItem("Produtos", $"{produtosVencidos} produto(s) com validade vencida", "/Produtos/Vencidos", "alta"));
        }

        var produtosVencendo = produtos.Count(p => p.Validade.Date >= hoje && p.Validade.Date <= hoje.AddDays(30));
        if (produtosVencendo > 0)
        {
            alertas.Add(new AlertaItem("Produtos", $"{produtosVencendo} produto(s) vencendo nos próximos 30 dias", "/Produtos/VencendoEm30Dias", "media"));
        }

        decimal? vendidoEsteMes = null;
        decimal? aReceber = null;
        decimal? aPagar = null;

        if (User.IsInRole("Admin"))
        {
            var pedidos = await _context.Pedidos.Include(p => p.Itens).ToListAsync();
            decimal ValorLiquidoPedido(Models.Pedido p) => Math.Round(p.Itens.Sum(i => i.Quantidade * i.ValorUnitario) * (1 - p.PercentualDesconto / 100), 2);

            var pedidosVencidos = pedidos.Count(p =>
                p.Status != "Cancelada" && p.Status != "Devolução" && !p.DataPagamento.HasValue &&
                p.DataVencimento.HasValue && p.DataVencimento.Value.Date < hoje);
            if (pedidosVencidos > 0)
            {
                alertas.Add(new AlertaItem("Financeiro", $"{pedidosVencidos} conta(s) a receber vencida(s)", "/Financeiro", "alta"));
            }

            vendidoEsteMes = pedidos
                .Where(p => p.Status == "Concluída" && p.DataPedido.Year == hoje.Year && p.DataPedido.Month == hoje.Month)
                .Sum(ValorLiquidoPedido);
            aReceber = pedidos
                .Where(p => p.Status != "Cancelada" && p.Status != "Devolução" && !p.DataPagamento.HasValue)
                .Sum(ValorLiquidoPedido);

            var compras = await _context.Compras.Include(c => c.Itens).ToListAsync();
            decimal ValorCompra(Models.Compra c) => c.Itens.Sum(i => i.Quantidade * i.ValorUnitario);

            var comprasVencidas = compras.Count(c =>
                c.Status != "Cancelada" && !c.DataPagamento.HasValue &&
                c.DataVencimento.HasValue && c.DataVencimento.Value.Date < hoje);
            if (comprasVencidas > 0)
            {
                alertas.Add(new AlertaItem("Financeiro", $"{comprasVencidas} conta(s) a pagar vencida(s)", "/Financeiro", "alta"));
            }

            aPagar = compras
                .Where(c => c.Status != "Cancelada" && !c.DataPagamento.HasValue)
                .Sum(ValorCompra);
        }

        return View(new DashboardViewModel(totalUsers, alertas, vendidoEsteMes, aReceber, aPagar));
    }
}

public sealed record AlertaItem(string Categoria, string Mensagem, string Url, string Severidade);

public sealed record DashboardViewModel(int TotalUsers, List<AlertaItem> Alertas, decimal? VendidoEsteMes, decimal? AReceber, decimal? APagar);
