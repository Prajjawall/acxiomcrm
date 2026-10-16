using AcxiomCrm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AcxiomCrm.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(u => u.ManagerId)
            .HasMaxLength(450);

        builder.Property(u => u.MustChangePassword)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(u => u.CreatedDate)
            .HasColumnType("datetime2(0)")
            .IsRequired();

        builder.Property(u => u.LastLoginDate)
            .HasColumnType("datetime2(0)");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(u => u.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AspNetUsers_ManagerId_NotSelf",
            "ManagerId IS NULL OR ManagerId <> Id"));
    }
}
