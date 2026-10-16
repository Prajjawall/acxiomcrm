using Microsoft.AspNetCore.Authorization;

namespace AcxiomCrm.Web.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
