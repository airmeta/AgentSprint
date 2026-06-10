using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Services;
using AgentSprint.Service.Services.SecurityServices;

namespace AgentSprint.Service.Impls.SecurityServices;

public sealed class SystemConfigurationService : AgentSprintServiceBase, ISystemConfigurationService
{
    private readonly ISystemConfigurationDomain _configurationDomain;

    /// <summary>
    /// zh-cn: 创建系统配置服务，用于维护可运行时调整的轻量配置项；配置项以唯一 Key 存储，业务服务读取时可提供默认值兜底。
    /// en-us: Creates the system configuration service for lightweight runtime-adjustable settings; settings are stored by unique keys and business services can provide default fallbacks when reading.
    /// </summary>
    /// <param name="configurationDomain">
    /// zh-cn: 系统配置领域对象，负责配置表的持久化访问。
    /// en-us: System-configuration domain object responsible for persistence access to the configuration table.
    /// </param>
    public SystemConfigurationService(ISystemConfigurationDomain configurationDomain)
    {
        _configurationDomain = configurationDomain;
    }

    /// <summary>
    /// zh-cn: 返回全部未删除配置项，按 Key 排序，供维护人员在系统配置页面查看和筛选。
    /// en-us: Returns all non-deleted settings ordered by key for maintainers to view and filter on the system-configuration page.
    /// </summary>
    /// <returns>
    /// zh-cn: 系统配置展示结果集合。
    /// en-us: System configuration display result collection.
    /// </returns>
    public async Task<IReadOnlyList<SystemConfigurationResult>> ListConfigurationsAsync(string? keyword = null, int? status = null)
    {
        var normalizedKeyword = NormalizeOptional(keyword);
        return (await _configurationDomain.ListAsync())
            .Where(entity =>
                (!status.HasValue || entity.Status == status.Value) &&
                (string.IsNullOrWhiteSpace(normalizedKeyword) ||
                    TextContains(normalizedKeyword, entity.Key, entity.Value, entity.Description)))
            .OrderBy(entity => entity.Key, StringComparer.Ordinal)
            .Select(MapConfiguration)
            .ToList();
    }

    /// <summary>
    /// zh-cn: 新增或更新系统配置项；同名 Key 会被视为同一配置并恢复软删除记录，避免数据库唯一索引冲突。
    /// en-us: Creates or updates a system setting; an existing key is treated as the same setting and soft-deleted rows are restored to avoid unique-index conflicts.
    /// </summary>
    /// <param name="request">
    /// zh-cn: 配置保存请求，Key 和 Value 必填，Description 可为空，Status 控制配置是否启用。
    /// en-us: Configuration save request; Key and Value are required, Description is optional, and Status controls whether the setting is enabled.
    /// </param>
    /// <returns>
    /// zh-cn: 保存后的配置项。
    /// en-us: Saved configuration result.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// zh-cn: Key 或 Value 为空，或请求 Id 与已有 Key 冲突时抛出。
    /// en-us: Thrown when Key or Value is empty, or when the request id conflicts with an existing key.
    /// </exception>
    public async Task<SystemConfigurationResult> UpsertConfigurationAsync(UpsertSystemConfigurationRequest request)
    {
        ValidateRequired(request.Key, "Configuration key is required.");
        ValidateRequired(request.Value, "Configuration value is required.");

        var key = request.Key.Trim();
        var entity = string.IsNullOrWhiteSpace(request.Id)
            ? null
            : await _configurationDomain.GetAsync(request.Id);
        var sameKey = await _configurationDomain.FindByKeyAsync(key);
        if (entity is not null && sameKey is not null && sameKey.Id != entity.Id)
        {
            throw new InvalidOperationException("Configuration key already exists.");
        }

        entity ??= sameKey ?? new SystemConfigurationEntity();
        entity.Key = key;
        entity.Value = request.Value.Trim();
        entity.Description = NormalizeOptional(request.Description);
        entity.Status = request.Status;
        entity.IsDelete = 0;

        if (sameKey is null && string.IsNullOrWhiteSpace(request.Id))
        {
            await _configurationDomain.CreateAsync(entity);
        }
        else
        {
            await _configurationDomain.UpdateAsync(entity);
        }

        return MapConfiguration(entity);
    }

    /// <summary>
    /// zh-cn: 软删除指定配置项；业务读取配置时会自动忽略已删除配置并回退默认值。
    /// en-us: Soft-deletes the specified setting; business reads ignore deleted settings and fall back to the provided default value.
    /// </summary>
    /// <param name="id">
    /// zh-cn: 配置项编号。
    /// en-us: Configuration id.
    /// </param>
    /// <returns>
    /// zh-cn: 删除操作结果。
    /// en-us: Deletion result.
    /// </returns>
    public Task<bool> DeleteConfigurationAsync(string id)
    {
        return _configurationDomain.DeleteAsync(id);
    }

    /// <summary>
    /// zh-cn: 读取启用状态的配置值；配置不存在、已禁用或值为空时返回调用方提供的默认值。
    /// en-us: Reads an enabled configuration value; returns the caller-provided default when the setting is missing, disabled, or empty.
    /// </summary>
    /// <param name="key">
    /// zh-cn: 配置键。
    /// en-us: Configuration key.
    /// </param>
    /// <param name="defaultValue">
    /// zh-cn: 配置不可用时使用的默认值。
    /// en-us: Default value used when the setting is unavailable.
    /// </param>
    /// <returns>
    /// zh-cn: 最终可用配置值。
    /// en-us: Effective configuration value.
    /// </returns>
    public async Task<string> GetValueAsync(string key, string defaultValue)
    {
        var entity = await _configurationDomain.FindByKeyAsync(key);
        if (entity is null || entity.Status == 0 || string.IsNullOrWhiteSpace(entity.Value))
        {
            return defaultValue;
        }

        return entity.Value.Trim();
    }

    private static SystemConfigurationResult MapConfiguration(SystemConfigurationEntity entity)
    {
        return new SystemConfigurationResult(entity.Id, entity.Key, entity.Value, entity.Description, entity.Status);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool TextContains(string keyword, params string?[] values)
    {
        return values.Any(value =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }
    }
}
