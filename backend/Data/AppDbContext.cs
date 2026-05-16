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

    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<Process> Processes => Set<Process>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<RoleDefinition> RoleDefinitions => Set<RoleDefinition>();

    public DbSet<Shopfloor> Shopfloors => Set<Shopfloor>();
    public DbSet<GlassSheet> GlassSheets => Set<GlassSheet>();
    public DbSet<SheetMovement> SheetMovements => Set<SheetMovement>();
    public DbSet<Batch> Batches => Set<Batch>();

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();

        b.Entity<User>().HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
        b.Entity<User>().HasIndex(u => new { u.TenantId, u.Provider, u.ProviderUserId });
        b.Entity<User>().HasIndex(u => new { u.TenantId, u.Number }).IsUnique();
        b.Entity<User>()
            .HasOne(u => u.Tenant).WithMany(t => t.Users).HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<RefreshToken>().HasIndex(t => t.TokenHash).IsUnique();
        b.Entity<RefreshToken>()
            .HasOne(t => t.User).WithMany(u => u.RefreshTokens).HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<PasswordResetToken>().HasIndex(t => t.TokenHash).IsUnique();
        b.Entity<PasswordResetToken>()
            .HasOne(t => t.User).WithMany(u => u.PasswordResetTokens).HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Plant>().HasIndex(p => new { p.TenantId, p.Code }).IsUnique();
        b.Entity<Plant>().HasIndex(p => new { p.TenantId, p.Number }).IsUnique();
        b.Entity<Plant>().HasOne(p => p.Tenant).WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Process>().HasIndex(p => new { p.TenantId, p.Code }).IsUnique();
        b.Entity<Process>().HasIndex(p => new { p.TenantId, p.Number }).IsUnique();
        b.Entity<Process>().HasOne(p => p.Tenant).WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Process>()
            .HasOne(p => p.Plant).WithMany(pl => pl.Processes).HasForeignKey(p => p.PlantId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Employee>().HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
        b.Entity<Employee>().HasIndex(e => new { e.TenantId, e.Number }).IsUnique();
        b.Entity<Employee>().HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Employee>().HasOne(e => e.Plant).WithMany().HasForeignKey(e => e.PlantId).OnDelete(DeleteBehavior.ClientSetNull);
        b.Entity<Employee>().HasOne(e => e.Process).WithMany().HasForeignKey(e => e.ProcessId).OnDelete(DeleteBehavior.ClientSetNull);

        b.Entity<Customer>().HasIndex(c => new { c.TenantId, c.Code }).IsUnique();
        b.Entity<Customer>().HasIndex(c => new { c.TenantId, c.Number }).IsUnique();
        b.Entity<Customer>().HasOne(c => c.Tenant).WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<RoleDefinition>().HasIndex(r => new { r.TenantId, r.Name }).IsUnique();
        b.Entity<RoleDefinition>().HasIndex(r => new { r.TenantId, r.Number }).IsUnique();
        b.Entity<RoleDefinition>().HasOne(r => r.Tenant).WithMany().HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Shopfloor>().HasIndex(s => new { s.TenantId, s.Code }).IsUnique();
        b.Entity<Shopfloor>().HasIndex(s => new { s.TenantId, s.Number }).IsUnique();
        b.Entity<Shopfloor>().HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Shopfloor>().HasOne(s => s.Process).WithMany().HasForeignKey(s => s.ProcessId).OnDelete(DeleteBehavior.ClientSetNull);

        b.Entity<GlassSheet>().HasIndex(g => new { g.TenantId, g.SheetNo }).IsUnique();
        b.Entity<GlassSheet>().HasIndex(g => new { g.TenantId, g.Number }).IsUnique();
        b.Entity<GlassSheet>().HasOne(g => g.Tenant).WithMany().HasForeignKey(g => g.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<GlassSheet>().HasOne(g => g.Customer).WithMany().HasForeignKey(g => g.CustomerId).OnDelete(DeleteBehavior.ClientSetNull);
        b.Entity<GlassSheet>()
            .HasOne(g => g.CurrentShopfloor).WithMany().HasForeignKey(g => g.CurrentShopfloorId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<GlassSheet>()
            .HasOne(g => g.Batch).WithMany(ba => ba.Sheets).HasForeignKey(g => g.BatchId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        b.Entity<Batch>().HasIndex(ba => new { ba.TenantId, ba.BatchNo }).IsUnique();
        b.Entity<Batch>().HasIndex(ba => new { ba.TenantId, ba.Number }).IsUnique();
        b.Entity<Batch>().HasIndex(ba => ba.CurrentShopfloorId);
        b.Entity<Batch>().HasOne(ba => ba.Tenant).WithMany().HasForeignKey(ba => ba.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Batch>()
            .HasOne(ba => ba.CurrentShopfloor).WithMany().HasForeignKey(ba => ba.CurrentShopfloorId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Plan>().HasIndex(p => p.Code).IsUnique();

        b.Entity<Subscription>().HasIndex(s => s.TenantId).IsUnique();
        b.Entity<Subscription>().HasOne(s => s.Tenant).WithOne(t => t.Subscription)
            .HasForeignKey<Subscription>(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Subscription>().HasOne(s => s.Plan).WithMany()
            .HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<SheetMovement>().HasIndex(m => m.GlassSheetId);
        // Restrict here — movements cascade-delete via their GlassSheet, so a second
        // cascade path from Tenant would trip SQL Server's "multiple cascade paths" rule.
        b.Entity<SheetMovement>().HasOne(m => m.Tenant).WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SheetMovement>()
            .HasOne(m => m.GlassSheet).WithMany(s => s.Movements).HasForeignKey(m => m.GlassSheetId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<SheetMovement>().HasOne(m => m.FromShopfloor).WithMany().HasForeignKey(m => m.FromShopfloorId).OnDelete(DeleteBehavior.ClientSetNull);
        b.Entity<SheetMovement>().HasOne(m => m.ToShopfloor).WithMany().HasForeignKey(m => m.ToShopfloorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SheetMovement>().HasOne(m => m.MovedByUser).WithMany().HasForeignKey(m => m.MovedByUserId).OnDelete(DeleteBehavior.ClientSetNull);
    }
}
