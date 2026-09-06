using Microsoft.EntityFrameworkCore;
using sassClaude.Models;

namespace sassClaude.Data;

public class SassDbContext : DbContext
{
    public SassDbContext(DbContextOptions<SassDbContext> options) : base(options)
    {
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Login>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Password).IsRequired().HasMaxLength(255);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(150);
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
    }
}
