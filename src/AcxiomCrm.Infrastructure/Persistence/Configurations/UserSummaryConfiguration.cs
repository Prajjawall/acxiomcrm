using AcxiomCrm.Application.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AcxiomCrm.Infrastructure.Persistence.Configurations;

public class UserSummaryConfiguration : IEntityTypeConfiguration<UserSummary>
{
    public void Configure(EntityTypeBuilder<UserSummary> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_UserSummary");
    }
}
