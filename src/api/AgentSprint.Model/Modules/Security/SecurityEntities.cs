using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using AgentSprint.Model.Modules.Common;

namespace AgentSprint.Model.Modules.Security;

[Table("sys_user")]
public sealed class UserEntity : EntityBase
{
    [MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? Email { get; set; }

    [MaxLength(32)]
    public string? PhoneNumber { get; set; }

    [MaxLength(512)]
    public string? Avatar { get; set; }

    public int Status { get; set; } = 1;
}

[Table("sys_role")]
public sealed class RoleEntity : EntityBase
{
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; set; }

    public int Status { get; set; } = 1;
}

[Table("sys_menu")]
public sealed class MenuEntity : EntityBase
{
    [MaxLength(64)]
    public string? ParentId { get; set; }

    [MaxLength(256)]
    public string Path { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Component { get; set; }

    [MaxLength(128)]
    public string? Icon { get; set; }

    public int Sort { get; set; }

    public int Type { get; set; }

    public int Status { get; set; } = 1;
}

[Table("sys_permission")]
public sealed class PermissionEntity : EntityBase
{
    [MaxLength(128)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? MenuId { get; set; }
}

[Table("sys_agent_token")]
public sealed class AgentTokenEntity : EntityBase
{
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    [MaxLength(128)]
    public string TokenValue { get; set; } = string.Empty;

    [MaxLength(8)]
    public string TokenPrefix { get; set; } = string.Empty;

    [MaxLength(8)]
    public string TokenSuffix { get; set; } = string.Empty;

    [MaxLength(64)]
    public string OwnerUserId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? ProjectId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    [MaxLength(64)]
    public string? RevokedBy { get; set; }

    public int Status { get; set; } = 1;
}

[Table("sys_configuration")]
public sealed class SystemConfigurationEntity : EntityBase
{
    [MaxLength(128)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string Value { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; set; }

    public int Status { get; set; } = 1;
}

[Table("sys_user_group")]
public sealed class UserGroupEntity : EntityBase
{
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; set; }

    public int Status { get; set; } = 1;
}

[Table("sys_role_group")]
public sealed class RoleGroupEntity : EntityBase
{
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; set; }

    public int Status { get; set; } = 1;
}

[Table("sys_department")]
public sealed class DepartmentEntity : EntityBase
{
    [MaxLength(64)]
    public string? ParentId { get; set; }

    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    public int Sort { get; set; }

    public int Status { get; set; } = 1;
}

[Table("assignment")]
public sealed class AssignmentEntity : EntityBase
{
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; set; }

    public int Status { get; set; } = 1;
}

[Table("sys_dictionary_type")]
public sealed class DictionaryTypeEntity : EntityBase
{
    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; set; }

    public int Sort { get; set; }

    public int Status { get; set; } = 1;
}

[Table("sys_dictionary_item")]
public sealed class DictionaryItemEntity : EntityBase
{
    [MaxLength(64)]
    public string DictionaryTypeId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; set; }

    public int Sort { get; set; }

    public int Status { get; set; } = 1;
}

[Table("sys_runtime_environment")]
public sealed class RuntimeEnvironmentEntity : EntityBase
{
    [MaxLength(64)]
    public string? ProjectId { get; set; }

    [MaxLength(64)]
    public string? EndpointId { get; set; }

    [MaxLength(64)]
    public string? ModuleId { get; set; }

    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(32)]
    public string EnvironmentType { get; set; } = "test";

    [MaxLength(1024)]
    public string? Description { get; set; }

    [MaxLength(512)]
    public string? FrontendUrl { get; set; }

    [MaxLength(512)]
    public string? ApiBaseUrl { get; set; }

    [MaxLength(512)]
    public string? FrontendProxyApiUrl { get; set; }

    [MaxLength(512)]
    public string? McpEndpoint { get; set; }

    [MaxLength(1024)]
    public string? ServerIps { get; set; }

    [MaxLength(512)]
    public string? DeployRoot { get; set; }

    [MaxLength(512)]
    public string? DockerDirectory { get; set; }

    [MaxLength(512)]
    public string? RemotePackagePath { get; set; }

    [MaxLength(512)]
    public string? ComposeFilePath { get; set; }

    [MaxLength(2048)]
    public string? LocalPackagePaths { get; set; }

    public int Sort { get; set; }

    public int Status { get; set; } = 1;
}

[Table("sys_runtime_environment_container")]
public sealed class RuntimeEnvironmentContainerEntity : EntityBase
{
    [MaxLength(64)]
    public string RuntimeEnvironmentId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    public int ContainerType { get; set; }

    [MaxLength(64)]
    public string? ServerIp { get; set; }

    public int HostPort { get; set; }

    public int ContainerPort { get; set; }

    [MaxLength(16)]
    public string Protocol { get; set; } = "tcp";

    [MaxLength(512)]
    public string? Description { get; set; }

    [Column(TypeName = "text")]
    public string? Prompt { get; set; }

    [Column(TypeName = "text")]
    public string? DeployScript { get; set; }

    public int Sort { get; set; }

    public int Status { get; set; } = 1;
}

[Table("sys_prompt_template")]
public sealed class PromptTemplateEntity : EntityBase
{
    [MaxLength(32)]
    public string AgentEnvironment { get; set; } = "codex";

    [MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(8192)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; set; }

    public int Sort { get; set; }

    public int Status { get; set; } = 1;
}

[Table("sys_entity_association")]
public sealed class EntityAssociationEntity : EntityBase
{
    [MaxLength(64)]
    public string SourceEntityId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string TargetEntityId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string AssociationType { get; set; } = string.Empty;
}

public static class SecurityAssociationTypes
{
    public const string UserUserGroup = "user_user_group";
    public const string UserRoleGroup = "user_role_group";
    public const string UserRole = "user_role";
    public const string UserDepartment = "user_department";
    public const string UserAssignment = "user_assignment";
    public const string UserGroupRoleGroup = "user_group_role_group";
    public const string UserGroupRole = "user_group_role";
    public const string RoleRoleGroup = "role_role_group";
    public const string RoleMenu = "role_menu";
    public const string RolePermission = "role_permission";
}

[Table("sys_user_role")]
public sealed class UserRoleEntity : EntityBase
{
    [MaxLength(64)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string RoleId { get; set; } = string.Empty;
}

[Table("sys_role_menu")]
public sealed class RoleMenuEntity : EntityBase
{
    [MaxLength(64)]
    public string RoleId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string MenuId { get; set; } = string.Empty;
}

[Table("sys_role_permission")]
public sealed class RolePermissionEntity : EntityBase
{
    [MaxLength(64)]
    public string RoleId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string PermissionId { get; set; } = string.Empty;
}
