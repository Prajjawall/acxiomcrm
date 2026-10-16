using AcxiomCrm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCrm.Web.Services;

public sealed class SessionSigner(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : ISessionSigner
{
    public async Task SignInAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user != null)
        {
            await signInManager.SignInAsync(user, isPersistent: false);
        }
    }

    public async Task SignOutAsync()
    {
        await signInManager.SignOutAsync();
    }
}
