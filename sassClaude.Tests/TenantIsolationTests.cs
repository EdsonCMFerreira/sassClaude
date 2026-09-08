using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using sassClaude.Data;
using sassClaude.Models;
using sassClaude.Services;

namespace sassClaude.Tests;

public sealed class TenantIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public TenantIsolationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private SassDbContext CreateContext(int? currentEmpresaId)
    {
        var options = new DbContextOptionsBuilder<SassDbContext>()
            .UseSqlite(_connection)
            .Options;
        var context = new SassDbContext(options, httpContextAccessor: null, new StubCurrentTenantAccessor(currentEmpresaId));
        context.Database.EnsureCreated();
        return context;
    }

    private async Task<(int EmpresaAId, int EmpresaBId)> SeedTwoEmpresasAsync()
    {
        using var setup = CreateContext(null);
        var empresaA = new Empresa { Nome = "Empresa A" };
        var empresaB = new Empresa { Nome = "Empresa B" };
        setup.Empresas.AddRange(empresaA, empresaB);
        await setup.SaveChangesAsync();
        return (empresaA.Id, empresaB.Id);
    }

    [Fact]
    public async Task Produtos_with_same_codigo_are_isolated_by_tenant()
    {
        var (empresaAId, empresaBId) = await SeedTwoEmpresasAsync();

        using (var contextA = CreateContext(empresaAId))
        {
            contextA.Produtos.Add(new Produto { Codigo = "COD-1", Descricao = "Produto A", ValorCompra = 1, ValorVenda = 2, Quantidade = 1 });
            await contextA.SaveChangesAsync();
        }

        using (var contextB = CreateContext(empresaBId))
        {
            contextB.Produtos.Add(new Produto { Codigo = "COD-1", Descricao = "Produto B", ValorCompra = 1, ValorVenda = 2, Quantidade = 1 });
            await contextB.SaveChangesAsync();
        }

        using var verifyA = CreateContext(empresaAId);
        var produtosA = await verifyA.Produtos.ToListAsync();
        Assert.Single(produtosA);
        Assert.Equal("Produto A", produtosA[0].Descricao);

        using var verifyB = CreateContext(empresaBId);
        var produtosB = await verifyB.Produtos.ToListAsync();
        Assert.Single(produtosB);
        Assert.Equal("Produto B", produtosB[0].Descricao);
    }

    [Fact]
    public async Task Clientes_with_same_cpfcnpj_are_isolated_by_tenant()
    {
        var (empresaAId, empresaBId) = await SeedTwoEmpresasAsync();

        using (var contextA = CreateContext(empresaAId))
        {
            contextA.Clientes.Add(new Cliente { Nome = "Cliente A", TipoPessoa = "Física", CpfCnpj = "111.111.111-11" });
            await contextA.SaveChangesAsync();
        }

        using (var contextB = CreateContext(empresaBId))
        {
            contextB.Clientes.Add(new Cliente { Nome = "Cliente B", TipoPessoa = "Física", CpfCnpj = "111.111.111-11" });
            await contextB.SaveChangesAsync();
        }

        using var verifyA = CreateContext(empresaAId);
        var clientesA = await verifyA.Clientes.ToListAsync();
        Assert.Single(clientesA);
        Assert.Equal("Cliente A", clientesA[0].Nome);

        using var verifyB = CreateContext(empresaBId);
        var clientesB = await verifyB.Clientes.ToListAsync();
        Assert.Single(clientesB);
        Assert.Equal("Cliente B", clientesB[0].Nome);
    }

    [Fact]
    public async Task Pedidos_numero_pedido_is_scoped_per_tenant()
    {
        var (empresaAId, empresaBId) = await SeedTwoEmpresasAsync();

        using (var contextA = CreateContext(empresaAId))
        {
            contextA.Pedidos.Add(new Pedido { NumeroPedido = 1, DataPedido = DateTime.UtcNow, Status = "Pendente" });
            await contextA.SaveChangesAsync();
        }

        using (var contextB = CreateContext(empresaBId))
        {
            contextB.Pedidos.Add(new Pedido { NumeroPedido = 1, DataPedido = DateTime.UtcNow, Status = "Pendente" });
            await contextB.SaveChangesAsync();
        }

        using var verifyA = CreateContext(empresaAId);
        Assert.Equal(1, await verifyA.Pedidos.CountAsync());

        using var verifyB = CreateContext(empresaBId);
        Assert.Equal(1, await verifyB.Pedidos.CountAsync());
    }

    [Fact]
    public async Task Anonymous_context_sees_logins_across_every_company()
    {
        var (empresaAId, empresaBId) = await SeedTwoEmpresasAsync();

        using (var setup = CreateContext(null))
        {
            setup.Logins.Add(new Login { EmpresaId = empresaAId, Username = "admin", Email = "a@a.com", Password = "x", Role = "Admin" });
            setup.Logins.Add(new Login { EmpresaId = empresaBId, Username = "admin", Email = "b@b.com", Password = "x", Role = "Admin" });
            await setup.SaveChangesAsync();
        }

        using var anonymous = CreateContext(null);
        Assert.Equal(2, await anonymous.Logins.CountAsync());

        using var scopedToA = CreateContext(empresaAId);
        var loginsForA = await scopedToA.Logins.ToListAsync();
        Assert.Single(loginsForA);
        Assert.Equal("a@a.com", loginsForA[0].Email);
    }

    [Fact]
    public async Task Login_identifier_lookup_by_username_alone_is_ambiguous_across_tenants()
    {
        var (empresaAId, empresaBId) = await SeedTwoEmpresasAsync();

        using (var setup = CreateContext(null))
        {
            setup.Logins.Add(new Login { EmpresaId = empresaAId, Username = "admin", Email = "a@a.com", Password = "x", Role = "Admin" });
            setup.Logins.Add(new Login { EmpresaId = empresaBId, Username = "admin", Email = "b@b.com", Password = "x", Role = "Admin" });
            await setup.SaveChangesAsync();
        }

        using var anonymous = CreateContext(null);

        // Mirrors AccountController.Login: email is still globally unique, so it resolves cleanly...
        var byEmail = await anonymous.Logins.FirstOrDefaultAsync(x => x.Email == "a@a.com");
        Assert.NotNull(byEmail);
        Assert.Equal(empresaAId, byEmail!.EmpresaId);

        // ...but Username is only unique per tenant, so a bare username lookup is ambiguous and must
        // never be resolved with FirstOrDefault (that would silently pick an arbitrary company).
        var byUsername = await anonymous.Logins.Where(x => x.Username == "admin").ToListAsync();
        Assert.Equal(2, byUsername.Count);
    }

    [Fact]
    public async Task IgnoreQueryFilters_bypasses_an_active_tenant_scope_for_cross_company_counts()
    {
        var (empresaAId, empresaBId) = await SeedTwoEmpresasAsync();

        using (var contextA = CreateContext(empresaAId))
        {
            contextA.Produtos.Add(new Produto { Codigo = "A-1", Descricao = "Produto A", ValorCompra = 1, ValorVenda = 2, Quantidade = 1 });
            await contextA.SaveChangesAsync();
        }

        using (var contextB = CreateContext(empresaBId))
        {
            contextB.Produtos.Add(new Produto { Codigo = "B-1", Descricao = "Produto B1", ValorCompra = 1, ValorVenda = 2, Quantidade = 1 });
            contextB.Produtos.Add(new Produto { Codigo = "B-2", Descricao = "Produto B2", ValorCompra = 1, ValorVenda = 2, Quantidade = 1 });
            await contextB.SaveChangesAsync();
        }

        // Scoped to empresaA — simulates a real superadmin session, which does carry its own EmpresaId claim.
        using var superAdminContext = CreateContext(empresaAId);

        Assert.Equal(1, await superAdminContext.Produtos.CountAsync()); // sanity: the active filter really is on

        var counts = await superAdminContext.Produtos.IgnoreQueryFilters()
            .GroupBy(p => p.EmpresaId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        Assert.Equal(1, counts[empresaAId]);
        Assert.Equal(2, counts[empresaBId]);
    }

    [Fact]
    public async Task SaveChanges_throws_when_no_tenant_is_resolved_and_entity_has_no_explicit_empresa()
    {
        using var anonymous = CreateContext(null);
        anonymous.Produtos.Add(new Produto { Codigo = "X", Descricao = "Sem tenant", ValorCompra = 1, ValorVenda = 2, Quantidade = 1 });

        await Assert.ThrowsAsync<InvalidOperationException>(() => anonymous.SaveChangesAsync());
    }

    private sealed class StubCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public StubCurrentTenantAccessor(int? empresaId) => EmpresaId = empresaId;

        public int? EmpresaId { get; }
    }
}
