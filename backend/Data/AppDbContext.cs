using Microsoft.EntityFrameworkCore;
using Tracker.Entities;

namespace Tracker.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();

        b.Entity<User>().HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
        b.Entity<User>().HasIndex(u => new { u.TenantId, u.Provider, u.ProviderUserId });
        b.Entity<User>()
            .HasOne(u => u.Tenant)
            .WithMany(t => t.Users)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<RefreshToken>().HasIndex(t => t.TokenHash).IsUnique();
        b.Entity<RefreshToken>()
            .HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<PasswordResetToken>().HasIndex(t => t.TokenHash).IsUnique();
        b.Entity<PasswordResetToken>()
            .HasOne(t => t.User)
            .WithMany(u => u.PasswordResetTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
