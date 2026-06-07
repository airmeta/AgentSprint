using AgentSprint.Domain.Impls.Common;
using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;

using Air.Cloud.EntityFrameWork.Core.Repositories;

namespace AgentSprint.Domain.Impls.Security;

public sealed class UserDomain : EntityDomainBase<UserEntity>, IUserDomain
{
    public UserDomain(IRepository<UserEntity> repository) : base(repository)
    {
    }

    public Task<UserEntity?> FindByUsernameAsync(string username)
    {
        return Repository.FirstOrDefaultAsync(entity =>
            entity.Username == username &&
            entity.IsDelete == 0 &&
            entity.Status == 1);
    }

    public Task<UserEntity?> FindAnyByUsernameAsync(string username)
    {
        return Repository.FirstOrDefaultAsync(entity => entity.Username == username);
    }
}

public sealed class RoleDomain : EntityDomainBase<RoleEntity>, IRoleDomain
{
    public RoleDomain(IRepository<RoleEntity> repository) : base(repository)
    {
    }

    public Task<RoleEntity?> FindAnyByCodeAsync(string code)
    {
        return Repository.FirstOrDefaultAsync(entity => entity.Code == code);
    }
}

public sealed class MenuDomain : EntityDomainBase<MenuEntity>, IMenuDomain
{
    public MenuDomain(IRepository<MenuEntity> repository) : base(repository)
    {
    }
}

public sealed class PermissionDomain : EntityDomainBase<PermissionEntity>, IPermissionDomain
{
    public PermissionDomain(IRepository<PermissionEntity> repository) : base(repository)
    {
    }
}

public sealed class AgentTokenDomain : EntityDomainBase<AgentTokenEntity>, IAgentTokenDomain
{
    public AgentTokenDomain(IRepository<AgentTokenEntity> repository) : base(repository)
    {
    }

    /// <summary>
    /// zh-cn: 按令牌哈希查找未软删除的 Agent 访问令牌记录，调用方负责继续判断状态、有效期和归属范围。
    /// en-us: Finds a non-deleted Agent access token by hash; callers remain responsible for status, expiry, and ownership checks.
    /// </summary>
    /// <param name="tokenHash">
    /// zh-cn: 由完整 64 位令牌计算得到的 SHA-256 十六进制哈希。
    /// en-us: SHA-256 hexadecimal hash calculated from the full 64-character token.
    /// </param>
    /// <returns>
    /// zh-cn: 匹配的令牌实体；不存在时返回 null。
    /// en-us: The matching token entity, or null when no token exists.
    /// </returns>
    public Task<AgentTokenEntity?> FindByTokenHashAsync(string tokenHash)
    {
        return Repository.FirstOrDefaultAsync(entity =>
            entity.TokenHash == tokenHash &&
            entity.IsDelete == 0);
    }
}

public sealed class SystemConfigurationDomain : EntityDomainBase<SystemConfigurationEntity>, ISystemConfigurationDomain
{
    public SystemConfigurationDomain(IRepository<SystemConfigurationEntity> repository) : base(repository)
    {
    }

    /// <summary>
    /// zh-cn: 按配置键查找未软删除的系统配置项；调用方负责根据业务需要处理禁用状态和默认值。
    /// en-us: Finds a non-deleted system configuration by key; callers handle disabled state and default fallback according to business needs.
    /// </summary>
    /// <param name="key">
    /// zh-cn: 配置键，使用模块化命名，例如 Mcp:Endpoint。
    /// en-us: Configuration key, using module-style naming such as Mcp:Endpoint.
    /// </param>
    /// <returns>
    /// zh-cn: 匹配的配置项；不存在时返回 null。
    /// en-us: Matching configuration entity, or null when it does not exist.
    /// </returns>
    public Task<SystemConfigurationEntity?> FindByKeyAsync(string key)
    {
        return Repository.FirstOrDefaultAsync(entity =>
            entity.Key == key &&
            entity.IsDelete == 0);
    }
}

public sealed class UserGroupDomain : EntityDomainBase<UserGroupEntity>, IUserGroupDomain
{
    public UserGroupDomain(IRepository<UserGroupEntity> repository) : base(repository)
    {
    }
}

public sealed class RoleGroupDomain : EntityDomainBase<RoleGroupEntity>, IRoleGroupDomain
{
    public RoleGroupDomain(IRepository<RoleGroupEntity> repository) : base(repository)
    {
    }
}

public sealed class DepartmentDomain : EntityDomainBase<DepartmentEntity>, IDepartmentDomain
{
    public DepartmentDomain(IRepository<DepartmentEntity> repository) : base(repository)
    {
    }
}

public sealed class AssignmentDomain : EntityDomainBase<AssignmentEntity>, IAssignmentDomain
{
    public AssignmentDomain(IRepository<AssignmentEntity> repository) : base(repository)
    {
    }
}

public sealed class EntityAssociationDomain : EntityDomainBase<EntityAssociationEntity>, IEntityAssociationDomain
{
    public EntityAssociationDomain(IRepository<EntityAssociationEntity> repository) : base(repository)
    {
    }
}

public sealed class UserRoleDomain : EntityDomainBase<UserRoleEntity>, IUserRoleDomain
{
    public UserRoleDomain(IRepository<UserRoleEntity> repository) : base(repository)
    {
    }
}

public sealed class RoleMenuDomain : EntityDomainBase<RoleMenuEntity>, IRoleMenuDomain
{
    public RoleMenuDomain(IRepository<RoleMenuEntity> repository) : base(repository)
    {
    }
}

public sealed class RolePermissionDomain : EntityDomainBase<RolePermissionEntity>, IRolePermissionDomain
{
    public RolePermissionDomain(IRepository<RolePermissionEntity> repository) : base(repository)
    {
    }
}
