using Microsoft.EntityFrameworkCore;
using ReliableWebhookDeliveryHub.Domain.Audit;
using ReliableWebhookDeliveryHub.Domain.Destinations;
using ReliableWebhookDeliveryHub.Domain.Tenants;

namespace ReliableWebhookDeliveryHub.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

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

        modelBuilder.Entity<Destination>(destination =>
        {
            destination.ToTable("Destinations");
            destination.HasKey(d => d.Id);
            destination.Property(d => d.TenantId)
                .IsRequired();
            destination.Property(d => d.Name)
                .IsRequired()
                .HasMaxLength(200);
            destination.Property(d => d.Url)
                .IsRequired()
                .HasMaxLength(2048);
            destination.Property(d => d.IsActive)
                .IsRequired();
            destination.Property(d => d.HeadersJson)
                .HasMaxLength(8000);
            destination.Property(d => d.SecretProtected)
                .HasMaxLength(4000);
            destination.Property(d => d.CreatedAt)
                .IsRequired();
            destination.Property(d => d.UpdatedAt)
                .IsRequired();
            destination.Property(d => d.RowVersion)
                .IsRequired()
                .IsRowVersion();

            destination.HasIndex(d => d.TenantId)
                .HasDatabaseName("IX_Destinations_TenantId");
            destination.HasIndex(d => new { d.TenantId, d.Name })
                .IsUnique();

            destination.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AuditLog>(auditLog =>
        {
            auditLog.ToTable("AuditLogs");
            auditLog.HasKey(a => a.Id);
            auditLog.Property(a => a.TenantId)
                .IsRequired();
            auditLog.Property(a => a.ActorType)
                .IsRequired()
                .HasMaxLength(50);
            auditLog.Property(a => a.ActorId)
                .IsRequired()
                .HasMaxLength(200);
            auditLog.Property(a => a.Action)
                .IsRequired()
                .HasMaxLength(100);
            auditLog.Property(a => a.EntityType)
                .IsRequired()
                .HasMaxLength(100);
            auditLog.Property(a => a.EntityId)
                .IsRequired();
            auditLog.Property(a => a.BeforeJson)
                .HasMaxLength(16000);
            auditLog.Property(a => a.AfterJson)
                .HasMaxLength(16000);
            auditLog.Property(a => a.CreatedAt)
                .IsRequired();

            auditLog.HasIndex(a => new { a.TenantId, a.CreatedAt })
                .HasDatabaseName("IX_AuditLogs_TenantId_CreatedAt");
            auditLog.HasIndex(a => new { a.EntityType, a.EntityId })
                .HasDatabaseName("IX_AuditLogs_EntityType_EntityId");

            auditLog.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(a => a.TenantId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        base.OnModelCreating(modelBuilder);
    }
}
