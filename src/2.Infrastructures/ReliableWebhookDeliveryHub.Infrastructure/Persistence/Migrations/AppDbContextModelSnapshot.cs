using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ReliableWebhookDeliveryHub.Infrastructure.Persistence;
using ReliableWebhookDeliveryHub.Domain.Audit;
using ReliableWebhookDeliveryHub.Domain.Destinations;
using ReliableWebhookDeliveryHub.Domain.Tenants;

namespace ReliableWebhookDeliveryHub.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
public partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            b.Property<string>("Action")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            b.Property<string>("ActorId")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            b.Property<string>("ActorType")
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnType("nvarchar(50)");

            b.Property<string>("AfterJson")
                .HasMaxLength(16000)
                .HasColumnType("nvarchar(max)");

            b.Property<string>("BeforeJson")
                .HasMaxLength(16000)
                .HasColumnType("nvarchar(max)");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("datetimeoffset");

            b.Property<Guid>("EntityId")
                .HasColumnType("uniqueidentifier");

            b.Property<string>("EntityType")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            b.Property<Guid>("TenantId")
                .HasColumnType("uniqueidentifier");

            b.HasKey("Id");

            b.HasIndex("TenantId", "CreatedAt")
                .HasDatabaseName("IX_AuditLogs_TenantId_CreatedAt");

            b.HasIndex("EntityType", "EntityId")
                .HasDatabaseName("IX_AuditLogs_EntityType_EntityId");

            b.ToTable("AuditLogs");

            b.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.NoAction)
                .IsRequired();
        });

        modelBuilder.Entity<Destination>(b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("datetimeoffset");

            b.Property<string>("HeadersJson")
                .HasMaxLength(8000)
                .HasColumnType("nvarchar(max)");

            b.Property<bool>("IsActive")
                .HasColumnType("bit");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            b.Property<byte[]>("RowVersion")
                .IsRequired()
                .IsRowVersion()
                .HasColumnType("rowversion");

            b.Property<string>("SecretProtected")
                .HasMaxLength(4000)
                .HasColumnType("nvarchar(4000)");

            b.Property<Guid>("TenantId")
                .HasColumnType("uniqueidentifier");

            b.Property<string>("Url")
                .IsRequired()
                .HasMaxLength(2048)
                .HasColumnType("nvarchar(2048)");

            b.Property<DateTimeOffset>("UpdatedAt")
                .HasColumnType("datetimeoffset");

            b.HasKey("Id");

            b.HasIndex("TenantId")
                .HasDatabaseName("IX_Destinations_TenantId");

            b.HasIndex("TenantId", "Name")
                .IsUnique();

            b.ToTable("Destinations");

            b.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.NoAction)
                .IsRequired();
        });

        modelBuilder.Entity<Tenant>(b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            b.Property<byte[]>("ApiKeyHash")
                .IsRequired()
                .HasColumnType("varbinary(max)");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("datetimeoffset");

            b.Property<bool>("IsActive")
                .HasColumnType("bit");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            b.Property<byte[]>("RowVersion")
                .IsRowVersion()
                .HasColumnType("rowversion");

            b.HasKey("Id");

            b.ToTable("Tenants");
        });
    }
}
