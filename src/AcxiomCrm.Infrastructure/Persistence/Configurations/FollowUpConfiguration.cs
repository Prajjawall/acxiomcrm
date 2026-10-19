using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AcxiomCrm.Infrastructure.Persistence.Configurations;

public class FollowUpConfiguration : IEntityTypeConfiguration<FollowUp>
{
    public void Configure(EntityTypeBuilder<FollowUp> builder)
    {
        builder.ToTable("FollowUps", t =>
        {
            t.HasCheckConstraint(
                "CK_FollowUps_ExactlyOneRelated",
                "(CASE WHEN CustomerId IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN LeadId IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN OpportunityId IS NOT NULL THEN 1 ELSE 0 END) = 1");
            t.HasCheckConstraint("CK_FollowUps_Status", "Status IN (1, 2, 3, 4)");
            t.HasCheckConstraint("CK_FollowUps_Type", "Type IN (1, 2, 3, 4, 5)");
        });

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .HasColumnName("FollowUpId");

        builder.Property(f => f.Subject)
            .HasMaxLength(Limits.Subject)
            .IsRequired();

        builder.Property(f => f.FollowUpDate)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(f => f.Type)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(f => f.Status)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(f => f.Remarks)
            .HasMaxLength(Limits.Notes);

        builder.Property(f => f.AssignedToId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(f => f.CompletedDate)
            .HasPrecision(0);

        builder.Property(f => f.RescheduleCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(f => f.IsHistoricalEntry)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(f => f.CreatedDate)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(f => f.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(f => f.ModifiedDate)
            .HasPrecision(0);

        builder.Property(f => f.ModifiedBy)
            .HasMaxLength(450);

        builder.Property(f => f.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(f => f.DeletedDate)
            .HasPrecision(0);

        builder.Property(f => f.DeletedBy)
            .HasMaxLength(450);

        builder.Property(f => f.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne(f => f.Customer)
            .WithMany()
            .HasForeignKey(f => f.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Lead)
            .WithMany()
            .HasForeignKey(f => f.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Opportunity)
            .WithMany()
            .HasForeignKey(f => f.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.AssignedToId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(f => !f.IsDeleted);

        builder.HasIndex(f => new { f.AssignedToId, f.FollowUpDate })
            .HasDatabaseName("IX_FollowUps_AssignedTo_FollowUpDate");

        builder.HasIndex(f => new { f.Status, f.FollowUpDate })
            .HasDatabaseName("IX_FollowUps_Status_FollowUpDate");

        builder.HasIndex(f => f.CustomerId)
            .HasDatabaseName("IX_FollowUps_CustomerId");

        builder.HasIndex(f => f.LeadId)
            .HasDatabaseName("IX_FollowUps_LeadId");

        builder.HasIndex(f => f.OpportunityId)
            .HasDatabaseName("IX_FollowUps_OpportunityId");
    }
}
