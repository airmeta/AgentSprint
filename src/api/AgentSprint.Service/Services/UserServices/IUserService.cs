using AgentSprint.Model.Modules.Security.Dtos;

namespace AgentSprint.Service.Services.UserServices;

public interface IUserService
{
    Task<CurrentUserResult> GetCurrentUserAsync(string userId);

    Task<IReadOnlyList<UserOptionResult>> ListUserOptionsAsync();

    Task<IReadOnlyList<MenuResult>> GetMenusAsync(string userId);
}
