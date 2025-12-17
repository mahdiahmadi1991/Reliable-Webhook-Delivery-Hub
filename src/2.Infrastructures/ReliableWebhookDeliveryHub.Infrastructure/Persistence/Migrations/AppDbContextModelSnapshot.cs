using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ReliableWebhookDeliveryHub.Infrastructure.Persistence;
using ReliableWebhookDeliveryHub.Domain.Tenants;

namespace ReliableWebhookDeliveryHub.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
public partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

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
