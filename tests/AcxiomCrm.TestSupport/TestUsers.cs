using AcxiomCrm.Infrastructure.Identity;
using AcxiomCrm.Infrastructure.Persistence;

namespace AcxiomCrm.TestSupport;

public sealed record StandardTestUsers(
    string AdminId,
    string NorthManagerId,
    string SouthManagerId,
    string ExecN1Id,
    string ExecN2Id,
    string ExecS1Id);

public static class TestUsers
{
    public static async Task<StandardTestUsers> CreateStandardAsync(CrmDbContext db)
    {
        var admin = new ApplicationUser
        {
            Id = "user-admin",
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            Email = "admin@acxiom.com",
            NormalizedEmail = "ADMIN@ACXIOM.COM",
            FullName = "Admin User",
            IsActive = true
        };

        var mgrNorth = new ApplicationUser
        {
            Id = "user-mgr-north",
            UserName = "mgr.north",
            NormalizedUserName = "MGR.NORTH",
            Email = "mgr.north@acxiom.com",
            NormalizedEmail = "MGR.NORTH@ACXIOM.COM",
            FullName = "North Manager",
            IsActive = true
        };

        var mgrSouth = new ApplicationUser
        {
            Id = "user-mgr-south",
            UserName = "mgr.south",
            NormalizedUserName = "MGR.SOUTH",
            Email = "mgr.south@acxiom.com",
            NormalizedEmail = "MGR.SOUTH@ACXIOM.COM",
            FullName = "South Manager",
            IsActive = true
        };

        var execN1 = new ApplicationUser
        {
            Id = "user-exec-n1",
            UserName = "exec.n1",
            NormalizedUserName = "EXEC.N1",
            Email = "exec.n1@acxiom.com",
            NormalizedEmail = "EXEC.N1@ACXIOM.COM",
            FullName = "Exec North 1",
            ManagerId = mgrNorth.Id,
            IsActive = true
        };

        var execN2 = new ApplicationUser
        {
            Id = "user-exec-n2",
            UserName = "exec.n2",
            NormalizedUserName = "EXEC.N2",
            Email = "exec.n2@acxiom.com",
            NormalizedEmail = "EXEC.N2@ACXIOM.COM",
            FullName = "Exec North 2 (Inactive)",
            ManagerId = mgrNorth.Id,
            IsActive = false // Test inactive direct reports
        };

        var execS1 = new ApplicationUser
        {
            Id = "user-exec-s1",
            UserName = "exec.s1",
            NormalizedUserName = "EXEC.S1",
            Email = "exec.s1@acxiom.com",
            NormalizedEmail = "EXEC.S1@ACXIOM.COM",
            FullName = "Exec South 1",
            ManagerId = mgrSouth.Id,
            IsActive = true
        };

        var adminRole = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.Roles, r => r.Name == AcxiomCrm.Domain.Security.Roles.Admin);
        if (adminRole == null)
        {
            adminRole = new ApplicationRole { Id = "role-admin", Name = AcxiomCrm.Domain.Security.Roles.Admin, NormalizedName = AcxiomCrm.Domain.Security.Roles.Admin.ToUpperInvariant() };
            var mgrRole = new ApplicationRole { Id = "role-manager", Name = AcxiomCrm.Domain.Security.Roles.Manager, NormalizedName = AcxiomCrm.Domain.Security.Roles.Manager.ToUpperInvariant() };
            var execRole = new ApplicationRole { Id = "role-exec", Name = AcxiomCrm.Domain.Security.Roles.SalesExecutive, NormalizedName = AcxiomCrm.Domain.Security.Roles.SalesExecutive.ToUpperInvariant() };
            db.Roles.AddRange(adminRole, mgrRole, execRole);
            await db.SaveChangesAsync();
        }

        var managerRole = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(db.Roles, r => r.Name == AcxiomCrm.Domain.Security.Roles.Manager);
        var salesExecRole = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(db.Roles, r => r.Name == AcxiomCrm.Domain.Security.Roles.SalesExecutive);

        db.Users.AddRange(admin, mgrNorth, mgrSouth, execN1, execN2, execS1);
        db.UserRoles.AddRange(
            new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = admin.Id, RoleId = adminRole.Id },
            new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = mgrNorth.Id, RoleId = managerRole.Id },
            new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = mgrSouth.Id, RoleId = managerRole.Id },
            new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = execN1.Id, RoleId = salesExecRole.Id },
            new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = execN2.Id, RoleId = salesExecRole.Id },
            new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = execS1.Id, RoleId = salesExecRole.Id });
        await db.SaveChangesAsync();

        return new StandardTestUsers(
            admin.Id,
            mgrNorth.Id,
            mgrSouth.Id,
            execN1.Id,
            execN2.Id,
            execS1.Id);
    }
}
