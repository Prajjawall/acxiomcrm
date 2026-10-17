using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AcxiomCrm.Infrastructure.Persistence.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads", t =>
        {
            t.HasCheckConstraint("CK_Leads_Contact", "Email IS NOT NULL OR Phone IS NOT NULL");
            t.HasCheckConstraint("CK_Leads_ExpectedValue", "ExpectedValue >= 0 AND ExpectedValue <= 100000000");
            t.HasCheckConstraint("CK_Leads_Converted", "(Status = 5 AND ConvertedCustomerId IS NOT NULL) OR (Status <> 5 AND ConvertedCustomerId IS NULL)");
            t.HasCheckConstraint("CK_Leads_Status", "Status IN (1, 2, 3, 4, 5, 6)");
            t.HasCheckConstraint("CK_Leads_Source", "Source IN (1, 2, 3, 4, 5, 6, 7)");
            t.HasCheckConstraint("CK_Leads_Priority", "Priority IN (1, 2, 3)");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("LeadId");

        builder.Property(l => l.Code)
            .HasColumnName("LeadCode")
            .HasMaxLength(Limits.Code)
            .IsRequired()
            .HasDefaultValueSql("'LEAD-' + FORMAT(NEXT VALUE FOR seq_LeadCode, '000000')")
            .ValueGeneratedOnAdd();

        builder.Property(l => l.Name)
            .HasColumnName("LeadName")
            .HasMaxLength(Limits.Name)
            .IsRequired();

        builder.Property(l => l.CompanyName)
            .HasMaxLength(Limits.Name);

        builder.Property(l => l.Email)
            .HasMaxLength(Limits.Email);

        builder.Property(l => l.EmailNormalized)
            .HasMaxLength(Limits.Email);

        builder.Property(l => l.Phone)
            .HasMaxLength(Limits.Phone);

        builder.Property(l => l.Source)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(l => l.Status)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(l => l.Priority)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(l => l.ExpectedValue)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(l => l.OwnerId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(l => l.Notes)
            .HasMaxLength(Limits.Notes);

        builder.Property(l => l.ConvertedCustomerId);

        builder.Property(l => l.ConvertedOpportunityId);

        builder.Property(l => l.ConvertedDate)
            .HasPrecision(0);

        builder.Property(l => l.CreatedDate)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(l => l.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(l => l.ModifiedDate)
            .HasPrecision(0);

        builder.Property(l => l.ModifiedBy)
            .HasMaxLength(450);

        builder.Property(l => l.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(l => l.DeletedDate)
            .HasPrecision(0);

        builder.Property(l => l.DeletedBy)
            .HasMaxLength(450);

        builder.Property(l => l.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(l => l.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.ConvertedCustomer)
            .WithMany()
            .HasForeignKey(l => l.ConvertedCustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(l => !l.IsDeleted);

        builder.HasIndex(l => l.Code)
            .IsUnique()
            .HasDatabaseName("UX_Leads_LeadCode");

        builder.HasIndex(l => new { l.OwnerId, l.Status })
            .HasDatabaseName("IX_Leads_OwnerId_Status");

        builder.HasIndex(l => new { l.Status, l.CreatedDate })
            .HasDatabaseName("IX_Leads_Status_CreatedDate");

        builder.HasIndex(l => l.Name)
            .HasDatabaseName("IX_Leads_LeadName");
    }
}
