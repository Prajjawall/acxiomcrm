using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcxiomCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserSummaryView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER VIEW vw_UserSummary AS
                SELECT 
                    u.Id,
                    u.FullName,
                    u.UserName,
                    u.Email,
                    ISNULL(r.Name, '') AS Role,
                    u.IsActive,
                    u.ManagerId,
                    CAST(CASE WHEN u.LockoutEnd IS NOT NULL AND u.LockoutEnd > SYSUTCDATETIME() THEN 1 ELSE 0 END AS bit) AS LockedOut,
                    u.LockoutEnd,
                    u.MustChangePassword,
                    u.CreatedDate,
                    u.LastLoginDate
                FROM AspNetUsers u
                LEFT JOIN AspNetUserRoles ur ON u.Id = ur.UserId
                LEFT JOIN AspNetRoles r ON ur.RoleId = r.Id
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_UserSummary");
        }
    }
}
