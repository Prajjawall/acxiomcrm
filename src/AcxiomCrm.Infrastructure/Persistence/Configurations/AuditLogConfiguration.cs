using AcxiomCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AcxiomCrm.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.AuditLogId);

        builder.Property(a => a.CreatedDate)
            .HasColumnType("datetime2(3)")
            .IsRequired();

        builder.Property(a => a.UserId)
            .HasMaxLength(450);

        builder.Property(a => a.UserName)
            .HasMaxLength(256);

        builder.Property(a => a.Module)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Action)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.EntityName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.RecordId)
            .HasMaxLength(64);

        builder.Property(a => a.Result)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(a => a.OldValue)
            .HasColumnType("nvarchar(max)");

        builder.Property(a => a.NewValue)
            .HasColumnType("nvarchar(max)");

        builder.Property(a => a.Details)
            .HasMaxLength(1000);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45);

        builder.Property(a => a.CorrelationId)
            .HasMaxLength(64);

        builder.HasIndex(a => a.CreatedDate)
            .HasDatabaseName("IX_AuditLogs_CreatedDate");

        builder.HasIndex(a => new { a.UserId, a.CreatedDate })
            .HasDatabaseName("IX_AuditLogs_UserId_CreatedDate");

        builder.HasIndex(a => new { a.Module, a.Action, a.CreatedDate })
            .HasDatabaseName("IX_AuditLogs_Module_Action_CreatedDate");

        builder.HasIndex(a => new { a.EntityName, a.RecordId })
            .HasDatabaseName("IX_AuditLogs_EntityName_RecordId");
    }
}
