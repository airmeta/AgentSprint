using AgentSprint.Model.Modules.Security.Dtos;

namespace AgentSprint.Service.Services.SecurityServices;

public interface ISecurityAuthorizationService
{
    Task<IReadOnlyList<string>> ResolveRoleIdsAsync(string userId);

    Task<IReadOnlyList<string>> ResolveRoleCodesAsync(string userId);

    Task<IReadOnlyList<string>> ResolvePermissionCodesAsync(string userId);

    Task<IReadOnlyList<MenuResult>> ResolveMenusAsync(string userId);
}
