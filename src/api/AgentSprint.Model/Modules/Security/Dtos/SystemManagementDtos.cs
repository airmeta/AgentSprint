namespace AgentSprint.Model.Modules.Security.Dtos;

public sealed record SecurityAssociationResult(
    string Id,
    string SourceEntityId,
    string TargetEntityId,
    string AssociationType);

public sealed record SecurityAssociationRequest(
    string SourceEntityId,
    string TargetEntityId,
    string AssociationType);

public sealed record UpsertUserRequest(
    string? Id,
    string Username,
    string DisplayName,
    string? Password,
    string? Email,
    string? PhoneNumber,
    string? Avatar,
    int Status,
    IReadOnlyList<string>? RoleIds);

public sealed record UserManagementResult(
    string Id,
    string Username,
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    string? Avatar,
    int Status,
    IReadOnlyList<string> RoleIds);

public sealed record UpsertRoleRequest(
    string? Id,
    string Code,
    string Name,
    string? Description,
    int Status,
    IReadOnlyList<string>? MenuIds,
    IReadOnlyList<string>? PermissionIds);

public sealed record RoleManagementResult(
    string Id,
    string Code,
    string Name,
    string? Description,
    int Status,
    IReadOnlyList<string> MenuIds,
    IReadOnlyList<string> PermissionIds);

public sealed record UpsertMenuRequest(
    string? Id,
    string? ParentId,
    string Path,
    string Name,
    string? Component,
    string? Icon,
    int Sort,
    int Type,
    int Status);

public sealed record MenuManagementResult(
    string Id,
    string? ParentId,
    string Path,
    string Name,
    string? Component,
    string? Icon,
    int Sort,
    int Type,
    int Status);

public sealed record UpsertPermissionRequest(
    string? Id,
    string Code,
    string Name,
    string? MenuId);

public sealed record PermissionManagementResult(
    string Id,
    string Code,
    string Name,
    string? MenuId);

public sealed record CreateAgentTokenRequest(
    string Name,
    DateTime ExpiresAt,
    string? ProjectId,
    string? OwnerUserId);

public sealed record AgentTokenManagementResult(
    string Id,
    string Name,
    string MaskedToken,
    string Token,
    string OwnerUserId,
    string OwnerUsername,
    string OwnerDisplayName,
    string CreatedBy,
    string CreatedByUsername,
    string? ProjectId,
    DateTime ExpiresAt,
    DateTime CreateTime,
    DateTime? LastUsedAt,
    DateTime? RevokedAt,
    string? RevokedBy,
    int Status);

public sealed record CreatedAgentTokenResult(
    string Id,
    string Token,
    AgentTokenManagementResult Metadata);

public sealed record AgentTokenValidationRequest(string Token);

public sealed record AgentTokenValidationResult(
    string AccessToken,
    string UserId,
    string Username,
    string DisplayName,
    string? ProjectId,
    IReadOnlyList<string> Roles);

public sealed record UpsertSystemConfigurationRequest(
    string? Id,
    string Key,
    string Value,
    string? Description,
    int Status);

public sealed record SystemConfigurationResult(
    string Id,
    string Key,
    string Value,
    string? Description,
    int Status);

public sealed record UpsertUserGroupRequest(
    string? Id,
    string Code,
    string Name,
    string? Description,
    int Status);

public sealed record UserGroupManagementResult(
    string Id,
    string Code,
    string Name,
    string? Description,
    int Status);

public sealed record UpsertRoleGroupRequest(
    string? Id,
    string Code,
    string Name,
    string? Description,
    int Status);

public sealed record RoleGroupManagementResult(
    string Id,
    string Code,
    string Name,
    string? Description,
    int Status);

public sealed record UpsertDepartmentRequest(
    string? Id,
    string? ParentId,
    string Code,
    string Name,
    int Sort,
    int Status);

public sealed record DepartmentManagementResult(
    string Id,
    string? ParentId,
    string Code,
    string Name,
    int Sort,
    int Status);

public sealed record UpsertAssignmentRequest(
    string? Id,
    string Code,
    string Name,
    string? Description,
    int Status);

public sealed record AssignmentManagementResult(
    string Id,
    string Code,
    string Name,
    string? Description,
    int Status);

public sealed record UpsertDictionaryTypeRequest(
    string? Id,
    string Code,
    string Name,
    string? Description,
    int Sort,
    int Status);

public sealed record DictionaryTypeManagementResult(
    string Id,
    string Code,
    string Name,
    string? Description,
    int Sort,
    int Status);

public sealed record UpsertDictionaryItemRequest(
    string? Id,
    string DictionaryTypeId,
    string Code,
    string Name,
    string? Description,
    int Sort,
    int Status);

public sealed record DictionaryItemManagementResult(
    string Id,
    string DictionaryTypeId,
    string Code,
    string Name,
    string? Description,
    int Sort,
    int Status);

public sealed record UpsertRuntimeEnvironmentRequest(
    string? Id,
    string? ProjectId,
    string? EndpointId,
    string? ModuleId,
    string Code,
    string Name,
    string EnvironmentType,
    string? Description,
    string? FrontendUrl,
    string? ApiBaseUrl,
    string? FrontendProxyApiUrl,
    string? McpEndpoint,
    string? ServerIps,
    string? DeployRoot,
    string? DockerDirectory,
    string? RemotePackagePath,
    string? ComposeFilePath,
    string? LocalPackagePaths,
    int Sort,
    int Status);

public sealed record RuntimeEnvironmentManagementResult(
    string Id,
    string? ProjectId,
    string? EndpointId,
    string? ModuleId,
    string Code,
    string Name,
    string EnvironmentType,
    string? Description,
    string? FrontendUrl,
    string? ApiBaseUrl,
    string? FrontendProxyApiUrl,
    string? McpEndpoint,
    string? ServerIps,
    string? DeployRoot,
    string? DockerDirectory,
    string? RemotePackagePath,
    string? ComposeFilePath,
    string? LocalPackagePaths,
    int Sort,
    int Status);

public sealed record UpsertRuntimeEnvironmentContainerRequest(
    string? Id,
    string RuntimeEnvironmentId,
    string Name,
    int ContainerType,
    string? ServerIp,
    int HostPort,
    int ContainerPort,
    string? Protocol,
    string? Description,
    int Sort,
    int Status,
    string? Prompt = null,
    string? DeployScript = null);

public sealed record RuntimeEnvironmentContainerManagementResult(
    string Id,
    string RuntimeEnvironmentId,
    string Name,
    int ContainerType,
    string? ServerIp,
    int HostPort,
    int ContainerPort,
    string Protocol,
    string? Description,
    int Sort,
    int Status,
    string? Prompt,
    string? DeployScript);

public sealed record UpsertPromptTemplateRequest(
    string? Id,
    string AgentEnvironment,
    string Code,
    string Name,
    string Content,
    string? Description,
    int Sort,
    int Status);

public sealed record PromptTemplateManagementResult(
    string Id,
    string AgentEnvironment,
    string Code,
    string Name,
    string Content,
    string? Description,
    int Sort,
    int Status);
