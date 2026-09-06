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
            entity.Property(x => x.Fornecedor).IsRequired().HasMaxLength(150);
            entity.Property(x => x.Valor).HasColumnType("decimal(10,2)");
            entity.HasIndex(x => x.Codigo).IsUnique();
        });
    }
}
