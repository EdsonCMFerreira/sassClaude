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
    }
}
