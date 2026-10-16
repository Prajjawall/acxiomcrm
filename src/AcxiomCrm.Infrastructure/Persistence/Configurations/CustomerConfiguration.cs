using AcxiomCrm.Domain.Entities;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AcxiomCrm.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", t =>
        {
            t.HasCheckConstraint("CK_Customers_Phone", "Phone LIKE '[6-9]%' AND LEN(Phone) = 10");
            t.HasCheckConstraint("CK_Customers_Status", "Status IN (1, 2)");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("CustomerId");

        builder.Property(c => c.Code)
            .HasColumnName("CustomerCode")
            .HasMaxLength(Limits.Code)
            .IsRequired()
            .HasDefaultValueSql("'CUS-' + FORMAT(NEXT VALUE FOR seq_CustomerCode, '000000')")
            .ValueGeneratedOnAdd();

        builder.Property(c => c.Name)
            .HasColumnName("CustomerName")
            .HasMaxLength(Limits.Name)
            .IsRequired();

        builder.Property(c => c.CompanyName)
            .HasMaxLength(Limits.Name);

        builder.Property(c => c.Email)
            .HasMaxLength(Limits.Email)
            .IsRequired();

        builder.Property(c => c.EmailNormalized)
            .HasMaxLength(Limits.Email)
            .IsRequired();

        builder.Property(c => c.Phone)
            .HasMaxLength(Limits.Phone)
            .IsRequired();

        builder.Property(c => c.Address)
            .HasMaxLength(Limits.Address);

        builder.Property(c => c.City)
            .HasMaxLength(Limits.CityState);

        builder.Property(c => c.State)
            .HasMaxLength(Limits.CityState);

        builder.Property(c => c.Status)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(c => c.OwnerId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(c => c.Notes)
            .HasMaxLength(Limits.Notes);

        builder.Property(c => c.CreatedDate)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(c => c.ModifiedDate)
            .HasPrecision(0);

        builder.Property(c => c.ModifiedBy)
            .HasMaxLength(450);

        builder.Property(c => c.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.DeletedDate)
            .HasPrecision(0);

        builder.Property(c => c.DeletedBy)
            .HasMaxLength(450);

        builder.Property(c => c.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasIndex(c => c.EmailNormalized)
            .IsUnique()
            .HasFilter("IsDeleted = 0")
            .HasDatabaseName("UX_Customers_EmailNormalized");

        builder.HasIndex(c => c.Phone)
            .IsUnique()
            .HasFilter("IsDeleted = 0")
            .HasDatabaseName("UX_Customers_Phone");

        builder.HasIndex(c => new { c.OwnerId, c.Status })
            .IncludeProperties(c => new { c.Name, c.CompanyName })
            .HasDatabaseName("IX_Customers_OwnerId_Status");

        builder.HasIndex(c => c.Name)
            .HasDatabaseName("IX_Customers_CustomerName");

        builder.HasIndex(c => c.Code)
            .IsUnique()
            .HasDatabaseName("UX_Customers_CustomerCode");
    }
}
