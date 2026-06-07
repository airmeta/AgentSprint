using AgentSprint.Model.Modules.Security.Dtos;

namespace AgentSprint.Service.Services.AuthServices;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request);

    Task<IReadOnlyList<string>> GetAccessCodesAsync(string userId);

    string CreateAccessToken(string userId, string username, IReadOnlyList<string> roles);
}
