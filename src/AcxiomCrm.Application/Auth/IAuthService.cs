using AcxiomCrm.Application.Common;

namespace AcxiomCrm.Application.Auth;

public interface IAuthService
{
    Task<AuthOutcome> AuthenticateAsync(LoginCommand cmd, CancellationToken ct = default);
    Task<Result<string>> RegisterAsync(RegisterCommand cmd, CancellationToken ct = default);
    Task<Result> ChangePasswordAsync(ChangePasswordCommand cmd, CancellationToken ct = default);
    Task RecordLogoutAsync(CancellationToken ct = default);
}
