using Microsoft.EntityFrameworkCore;
using sassClaude.Models;

namespace sassClaude.Data;

public class SassDbContext : DbContext
{
    public SassDbContext(DbContextOptions<SassDbContext> options) : base(options)
    {
    }

    public DbSet<Login> Logins => Set<Login>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Login>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Password).IsRequired().HasMaxLength(255);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(150);
        });
    }
}
