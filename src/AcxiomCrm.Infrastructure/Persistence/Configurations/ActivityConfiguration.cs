using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AcxiomCrm.Infrastructure.Persistence.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("Activities", t =>
        {
            t.HasCheckConstraint(
                "CK_Activities_AtLeastOneRelated",
                "(CASE WHEN CustomerId IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN LeadId IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN OpportunityId IS NOT NULL THEN 1 ELSE 0 END) >= 1");
            t.HasCheckConstraint("CK_Activities_Status", "Status IN (1, 2, 3)");
            t.HasCheckConstraint("CK_Activities_Type", "Type IN (1, 2, 3, 4)");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("ActivityId");

        builder.Property(a => a.Type)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(a => a.Subject)
            .HasMaxLength(Limits.Subject)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasMaxLength(Limits.Notes);

        builder.Property(a => a.ActivityDate)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(a => a.AssignedToId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(a => a.CreatedDate)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(a => a.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(a => a.ModifiedDate)
            .HasPrecision(0);

        builder.Property(a => a.ModifiedBy)
            .HasMaxLength(450);

        builder.Property(a => a.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(a => a.DeletedDate)
            .HasPrecision(0);

        builder.Property(a => a.DeletedBy)
            .HasMaxLength(450);

        builder.Property(a => a.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne(a => a.Customer)
            .WithMany()
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Lead)
            .WithMany()
            .HasForeignKey(a => a.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Opportunity)
            .WithMany()
            .HasForeignKey(a => a.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.AssignedToId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.HasIndex(a => new { a.AssignedToId, a.ActivityDate })
            .HasDatabaseName("IX_Activities_AssignedTo_ActivityDate");

        builder.HasIndex(a => a.CustomerId)
            .HasDatabaseName("IX_Activities_CustomerId");

        builder.HasIndex(a => a.LeadId)
            .HasDatabaseName("IX_Activities_LeadId");

        builder.HasIndex(a => a.OpportunityId)
            .HasDatabaseName("IX_Activities_OpportunityId");
    }
}
