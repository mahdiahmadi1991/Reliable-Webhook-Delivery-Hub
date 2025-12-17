using Microsoft.EntityFrameworkCore;
using ReliableWebhookDeliveryHub.Domain.Tenants;

namespace ReliableWebhookDeliveryHub.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(tenant =>
        {
            tenant.ToTable("Tenants");
            tenant.HasKey(t => t.Id);
            tenant.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(200);
            tenant.Property(t => t.ApiKeyHash)
                .IsRequired();
            tenant.Property(t => t.CreatedAt)
                .IsRequired();
            tenant.Property(t => t.RowVersion)
                .IsRowVersion();
        });

        base.OnModelCreating(modelBuilder);
    }
}
