using AgentSprint.Model.Modules.Security.Dtos;

namespace AgentSprint.Service.Services.SecurityServices;

public interface ISystemConfigurationService
{
    Task<IReadOnlyList<SystemConfigurationResult>> ListConfigurationsAsync(string? keyword = null, int? status = null);

    Task<SystemConfigurationResult> UpsertConfigurationAsync(UpsertSystemConfigurationRequest request);

    Task<bool> DeleteConfigurationAsync(string id);

    Task<string> GetValueAsync(string key, string defaultValue);
}
