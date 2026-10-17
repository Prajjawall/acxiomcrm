using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AcxiomCrm.Infrastructure.Persistence.Configurations;

public class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.ToTable("Opportunities", t =>
        {
            t.HasCheckConstraint("CK_Opps_Amount", "Amount >= 0 AND Amount <= 1000000000");
            t.HasCheckConstraint("CK_Opps_OpenAmountPositive", "Status <> 1 OR Amount > 0");
            t.HasCheckConstraint("CK_Opps_Probability", "Probability >= 0 AND Probability <= 100");
            t.HasCheckConstraint("CK_Opps_StageStatus", "(Stage IN (1, 2, 3) AND Status = 1) OR (Stage = 4 AND Status = 2) OR (Stage = 5 AND Status = 3)");
            t.HasCheckConstraint("CK_Opps_ClosedDate", "(Status = 1 AND ClosedDate IS NULL) OR (Status <> 1 AND ClosedDate IS NOT NULL)");
            t.HasCheckConstraint("CK_Opps_Stage", "Stage IN (1, 2, 3, 4, 5)");
            t.HasCheckConstraint("CK_Opps_Status", "Status IN (1, 2, 3)");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasColumnName("OpportunityId");

        builder.Property(o => o.Name)
            .HasColumnName("OpportunityName")
            .HasMaxLength(Limits.Name)
            .IsRequired();

        builder.Property(o => o.CustomerId)
            .IsRequired();

        builder.Property(o => o.LeadId);

        builder.Property(o => o.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(o => o.Stage)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(o => o.Status)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(o => o.Probability)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(o => o.ExpectedCloseDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(o => o.ClosedDate)
            .HasColumnType("date");

        builder.Property(o => o.CloseNote)
            .HasMaxLength(Limits.CloseReason);

        builder.Property(o => o.Source)
            .HasColumnType("tinyint");

        builder.Property(o => o.OwnerId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(o => o.Notes)
            .HasMaxLength(Limits.Notes);

        builder.Property(o => o.CreatedDate)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(o => o.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(o => o.ModifiedDate)
            .HasPrecision(0);

        builder.Property(o => o.ModifiedBy)
            .HasMaxLength(450);

        builder.Property(o => o.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(o => o.DeletedDate)
            .HasPrecision(0);

        builder.Property(o => o.DeletedBy)
            .HasMaxLength(450);

        builder.Property(o => o.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Lead)
            .WithMany()
            .HasForeignKey(o => o.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(o => o.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(o => !o.IsDeleted);

        builder.HasIndex(o => new { o.OwnerId, o.Status })
            .HasDatabaseName("IX_Opps_OwnerId_Status");

        builder.HasIndex(o => o.CustomerId)
            .HasDatabaseName("IX_Opps_CustomerId");

        builder.HasIndex(o => new { o.Stage, o.ExpectedCloseDate })
            .HasDatabaseName("IX_Opps_Stage_ExpectedCloseDate");

        builder.HasIndex(o => new { o.Status, o.ClosedDate })
            .HasDatabaseName("IX_Opps_Status_ClosedDate");
    }
}
