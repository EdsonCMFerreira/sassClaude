using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Saas.Models;
using Saas.Services;

namespace Saas.Data;

public class SassDbContext : DbContext
{
    private static readonly Type[] AuditableTypes =
    [
        typeof(Produto), typeof(Cliente), typeof(Fornecedor), typeof(Pedido), typeof(Login), typeof(Projeto)
    ];

    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ICurrentTenantAccessor? _currentTenantAccessor;

    public SassDbContext(DbContextOptions<SassDbContext> options, IHttpContextAccessor? httpContextAccessor = null, ICurrentTenantAccessor? currentTenantAccessor = null) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
        _currentTenantAccessor = currentTenantAccessor;
    }

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Login> Logins => Set<Login>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<WorkspaceInvite> WorkspaceInvites => Set<WorkspaceInvite>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<PedidoItem> PedidoItens => Set<PedidoItem>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<Projeto> Projetos => Set<Projeto>();
    public DbSet<Tarefa> Tarefas => Set<Tarefa>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        return SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State != EntityState.Added || entry.Entity.EmpresaId != 0)
            {
                continue;
            }

            entry.Entity.EmpresaId = _currentTenantAccessor?.EmpresaId
                ?? throw new InvalidOperationException($"Tenant não identificado ao salvar {entry.Entity.GetType().Name}.");
        }

        var pending = new List<(EntityEntry Entry, string EntityName, string Action, string Details, int EmpresaId)>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (!AuditableTypes.Contains(entry.Entity.GetType()))
            {
                continue;
            }

            var entityName = entry.Entity.GetType().Name;
            var action = entry.State switch
            {
                EntityState.Added => "Criação",
                EntityState.Modified => "Atualização",
                EntityState.Deleted => "Exclusão",
                _ => "Desconhecida"
            };

            if (action == "Atualização" && !entry.Properties.Any(p => p.IsModified && p.Metadata.Name != "Id"))
            {
                continue;
            }

            var entityEmpresaId = Convert.ToInt32(entry.Property(nameof(ITenantScoped.EmpresaId)).CurrentValue);
            pending.Add((entry, entityName, action, BuildDetails(entry, action), entityEmpresaId));
        }

        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        if (pending.Count > 0)
        {
            var userName = _httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "Sistema";
            foreach (var item in pending)
            {
                var entityId = Convert.ToInt32(item.Entry.Property("Id").CurrentValue);
                AuditLogEntries.Add(new AuditLogEntry
                {
                    EmpresaId = item.EmpresaId,
                    EntityName = item.EntityName,
                    EntityId = entityId,
                    Action = item.Action,
                    UserName = userName,
                    Details = item.Details,
                    Timestamp = DateTime.UtcNow
                });
            }

            await base.SaveChangesAsync(true, cancellationToken);
        }

        return result;
    }

    private static string BuildDetails(EntityEntry entry, string action)
    {
        if (action != "Atualização")
        {
            return string.Empty;
        }

        var changes = entry.Properties
            .Where(p => p.IsModified && p.Metadata.Name != "Id")
            .Select(p => p.Metadata.Name == "Password"
                ? "Password: (alterada)"
                : $"{p.Metadata.Name}: '{p.OriginalValue}' → '{p.CurrentValue}'");

        return string.Join("; ", changes);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Empresa>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nome).IsRequired().HasMaxLength(150);
            entity.Property(x => x.Plano).IsRequired().HasMaxLength(20);
        });

        modelBuilder.Entity<Login>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Password).IsRequired().HasMaxLength(255);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(150);
            entity.Property(x => x.Role).IsRequired().HasMaxLength(20);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => new { x.EmpresaId, x.Username }).IsUnique();
            entity.HasQueryFilter(x => !_currentTenantAccessor!.EmpresaId.HasValue || x.EmpresaId == _currentTenantAccessor!.EmpresaId);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasOne(x => x.Login)
                .WithMany()
                .HasForeignKey(x => x.LoginId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailVerificationToken>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasOne(x => x.Login)
                .WithMany()
                .HasForeignKey(x => x.LoginId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkspaceInvite>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(150);
            entity.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasOne(x => x.InvitedBy)
                .WithMany()
                .HasForeignKey(x => x.InvitedByLoginId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x => !_currentTenantAccessor!.EmpresaId.HasValue || x.EmpresaId == _currentTenantAccessor!.EmpresaId);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).IsRequired().HasMaxLength(30);
            entity.Property(x => x.Description).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Amount).HasColumnType("decimal(10,2)");
            entity.HasOne(x => x.Login)
                .WithMany()
                .HasForeignKey(x => x.LoginId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x => !_currentTenantAccessor!.EmpresaId.HasValue || x.EmpresaId == _currentTenantAccessor!.EmpresaId);
        });

        modelBuilder.Entity<Produto>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Codigo).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Descricao).IsRequired().HasMaxLength(200);
            entity.Property(x => x.ValorCompra).HasColumnType("decimal(10,2)");
            entity.Property(x => x.ValorVenda).HasColumnType("decimal(10,2)");
            entity.HasIndex(x => new { x.EmpresaId, x.Codigo }).IsUnique();
            entity.HasOne(x => x.Fornecedor)
                .WithMany()
                .HasForeignKey(x => x.FornecedorId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(x => !_currentTenantAccessor!.EmpresaId.HasValue || x.EmpresaId == _currentTenantAccessor!.EmpresaId);
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nome).IsRequired().HasMaxLength(150);
            entity.Property(x => x.TipoPessoa).IsRequired().HasMaxLength(20);
            entity.Property(x => x.CpfCnpj).IsRequired().HasMaxLength(20);
            entity.Property(x => x.UltimaCompraValor).HasColumnType("decimal(10,2)");
            entity.HasIndex(x => new { x.EmpresaId, x.CpfCnpj }).IsUnique();
            entity.HasQueryFilter(x => !_currentTenantAccessor!.EmpresaId.HasValue || x.EmpresaId == _currentTenantAccessor!.EmpresaId);
        });

        modelBuilder.Entity<Fornecedor>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nome).IsRequired().HasMaxLength(150);
            entity.Property(x => x.TipoPessoa).IsRequired().HasMaxLength(20);
            entity.Property(x => x.CpfCnpj).IsRequired().HasMaxLength(20);
            entity.HasIndex(x => new { x.EmpresaId, x.CpfCnpj }).IsUnique();
            entity.HasQueryFilter(x => !_currentTenantAccessor!.EmpresaId.HasValue || x.EmpresaId == _currentTenantAccessor!.EmpresaId);
        });

        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NumeroPedido).IsRequired();
            entity.Property(x => x.Status).IsRequired().HasMaxLength(20);
            entity.Property(x => x.PercentualDesconto).HasColumnType("decimal(5,2)");
            entity.HasIndex(x => new { x.EmpresaId, x.NumeroPedido }).IsUnique();
            entity.HasOne(x => x.Cliente)
                .WithMany()
                .HasForeignKey(x => x.ClienteId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(x => !_currentTenantAccessor!.EmpresaId.HasValue || x.EmpresaId == _currentTenantAccessor!.EmpresaId);
        });

        modelBuilder.Entity<PedidoItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ValorUnitario).HasColumnType("decimal(10,2)");
            entity.HasOne(x => x.Pedido)
                .WithMany(x => x.Itens)
                .HasForeignKey(x => x.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Produto)
                .WithMany()
                .HasForeignKey(x => x.ProdutoId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(x => !_currentTenantAccessor!.EmpresaId.HasValue || x.EmpresaId == _currentTenantAccessor!.EmpresaId);
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntityName).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Action).IsRequired().HasMaxLength(20);
            entity.Property(x => x.UserName).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Details).IsRequired().HasMaxLength(1000);
            entity.HasQueryFilter(x => !_currentTenantAccessor!.EmpresaId.HasValue || x.EmpresaId == _currentTenantAccessor!.EmpresaId);
        });

        modelBuilder.Entity<Projeto>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nome).IsRequired().HasMaxLength(150);
            entity.Property(x => x.Descricao).IsRequired().HasMaxLength(1000);
            entity.HasQueryFilter(x => !_currentTenantAccessor!.EmpresaId.HasValue || x.EmpresaId == _currentTenantAccessor!.EmpresaId);
        });

        modelBuilder.Entity<Tarefa>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            entity.HasOne(x => x.Projeto)
                .WithMany(x => x.Tarefas)
                .HasForeignKey(x => x.ProjetoId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x => !_currentTenantAccessor!.EmpresaId.HasValue || x.EmpresaId == _currentTenantAccessor!.EmpresaId);
        });
    }
}
