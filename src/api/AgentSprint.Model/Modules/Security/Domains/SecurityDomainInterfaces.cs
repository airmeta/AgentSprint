using System.Linq.Expressions;

using AgentSprint.Model.Modules.Common;

using Air.Cloud.Core.Standard.DataBase.Domains;
using Air.Cloud.Core.Standard.DynamicServer;

namespace AgentSprint.Model.Modules.Security.Domains;

public interface IEntityDomainBase<TEntity>
    : IEntityDomain, ITransient
    where TEntity : EntityBase, new()
{
    Task<string> CreateAsync(TEntity entity);

    Task<TEntity?> GetAsync(string id);

    Task<IList<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null);

    Task<IList<TEntity>> ListIncludingDeletedAsync(Expression<Func<TEntity, bool>>? predicate = null);

    Task<string> UpdateAsync(TEntity entity);

    Task<bool> DeleteAsync(string id);
}

public interface IUserDomain : IEntityDomainBase<UserEntity>
{
    Task<UserEntity?> FindByUsernameAsync(string username);

    Task<UserEntity?> FindAnyByUsernameAsync(string username);
}

public interface IRoleDomain : IEntityDomainBase<RoleEntity>
{
    Task<RoleEntity?> FindAnyByCodeAsync(string code);
}

public interface IMenuDomain : IEntityDomainBase<MenuEntity>;

public interface IPermissionDomain : IEntityDomainBase<PermissionEntity>;

public interface IAgentTokenDomain : IEntityDomainBase<AgentTokenEntity>
{
    Task<AgentTokenEntity?> FindByTokenHashAsync(string tokenHash);
}

public interface ISystemConfigurationDomain : IEntityDomainBase<SystemConfigurationEntity>
{
    Task<SystemConfigurationEntity?> FindByKeyAsync(string key);
}

public interface IUserGroupDomain : IEntityDomainBase<UserGroupEntity>;

public interface IRoleGroupDomain : IEntityDomainBase<RoleGroupEntity>;

public interface IDepartmentDomain : IEntityDomainBase<DepartmentEntity>;

public interface IAssignmentDomain : IEntityDomainBase<AssignmentEntity>;

public interface IEntityAssociationDomain : IEntityDomainBase<EntityAssociationEntity>;

public interface IUserRoleDomain : IEntityDomainBase<UserRoleEntity>;

public interface IRoleMenuDomain : IEntityDomainBase<RoleMenuEntity>;

public interface IRolePermissionDomain : IEntityDomainBase<RolePermissionEntity>;
