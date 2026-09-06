using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using sassClaude.Models;

namespace sassClaude.Data;

public class SassDbContext : DbContext
{
    private static readonly Type[] AuditableTypes =
    [
        typeof(Product), typeof(Cliente), typeof(Fornecedor), typeof(Compra), typeof(Venda), typeof(Login)
    ];

    private readonly IHttpContextAccessor? _httpContextAccessor;

    public SassDbContext(DbContextOptions<SassDbContext> options, IHttpContextAccessor? httpContextAccessor = null) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<Login> Logins => Set<Login>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<WorkspaceInvite> WorkspaceInvites => Set<WorkspaceInvite>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<Venda> Vendas => Set<Venda>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        return SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var pending = new List<(EntityEntry Entry, string EntityName, string Action, string Details)>();

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

            pending.Add((entry, entityName, action, BuildDetails(entry, action)));
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
        modelBuilder.Entity<Login>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Password).IsRequired().HasMaxLength(255);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(150);
            entity.Property(x => x.Role).IsRequired().HasMaxLength(20);
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
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Codigo).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Descricao).IsRequired().HasMaxLength(200);
            entity.Property(x => x.ValorCompra).HasColumnType("decimal(10,2)");
            entity.Property(x => x.ValorVenda).HasColumnType("decimal(10,2)");
            entity.HasIndex(x => x.Codigo).IsUnique();
            entity.HasOne(x => x.Fornecedor)
                .WithMany()
                .HasForeignKey(x => x.FornecedorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nome).IsRequired().HasMaxLength(150);
            entity.Property(x => x.TipoPessoa).IsRequired().HasMaxLength(20);
            entity.Property(x => x.CpfCnpj).IsRequired().HasMaxLength(20);
            entity.Property(x => x.UltimaCompraValor).HasColumnType("decimal(10,2)");
            entity.HasIndex(x => x.CpfCnpj).IsUnique();
        });

        modelBuilder.Entity<Fornecedor>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nome).IsRequired().HasMaxLength(150);
            entity.Property(x => x.TipoPessoa).IsRequired().HasMaxLength(20);
            entity.Property(x => x.CpfCnpj).IsRequired().HasMaxLength(20);
            entity.Property(x => x.UltimaCompraValor).HasColumnType("decimal(10,2)");
            entity.HasIndex(x => x.CpfCnpj).IsUnique();
        });

        modelBuilder.Entity<Compra>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).IsRequired().HasMaxLength(20);
            entity.Property(x => x.ValorUnitario).HasColumnType("decimal(10,2)");
            entity.HasOne(x => x.Fornecedor)
                .WithMany()
                .HasForeignKey(x => x.FornecedorId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Venda>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).IsRequired().HasMaxLength(20);
            entity.Property(x => x.ValorUnitario).HasColumnType("decimal(10,2)");
            entity.HasOne(x => x.Cliente)
                .WithMany()
                .HasForeignKey(x => x.ClienteId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntityName).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Action).IsRequired().HasMaxLength(20);
            entity.Property(x => x.UserName).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Details).IsRequired().HasMaxLength(1000);
        });
    }
}
