namespace AcxiomCrm.Web.Services;

public interface ISessionSigner
{
    Task SignInAsync(string userId, CancellationToken ct = default);
    Task SignOutAsync();
}
