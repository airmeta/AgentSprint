using AgentSprint.Model.Modules.Security;
using AgentSprint.Service.Impls.SecurityServices;
using AgentSprint.Model.Modules.Security.Dtos;

namespace AgentSprint.Tests;

public sealed class SystemManagementServiceTests
{
    [Fact]
    public async Task UpsertUserAsync_SyncsGenericAssociationAndLegacyUserRoleRows()
    {
        var users = new List<UserEntity>();
        var userRoles = new List<UserRoleEntity>();
        var associations = new List<EntityAssociationEntity>();
        var service = CreateService(users: users, userRoles: userRoles, associations: associations);

        var user = await service.UpsertUserAsync(new UpsertUserRequest(
            null,
            "maintainer",
            "Maintainer",
            "123456",
            null,
            null,
            null,
            1,
            ["role-1", "role-2"]));

        Assert.Equal(["role-1", "role-2"], user.RoleIds.OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(
            ["role-1", "role-2"],
            associations
                .Where(entity => entity.SourceEntityId == user.Id && entity.AssociationType == SecurityAssociationTypes.UserRole && entity.IsDelete == 0)
                .Select(entity => entity.TargetEntityId)
                .OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(
            ["role-1", "role-2"],
            userRoles
                .Where(entity => entity.UserId == user.Id && entity.IsDelete == 0)
                .Select(entity => entity.RoleId)
                .OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task UpsertUserAsync_RestoresSoftDeletedUserRoleAssociation()
    {
        var user = new UserEntity
        {
            Id = "user-1",
            Username = "maintainer",
            DisplayName = "Maintainer",
            PasswordHash = "existing"
        };
        var userRole = new UserRoleEntity { UserId = user.Id, RoleId = "role-1", IsDelete = 1 };
        var association = new EntityAssociationEntity
        {
            SourceEntityId = user.Id,
            TargetEntityId = "role-1",
            AssociationType = SecurityAssociationTypes.UserRole,
            IsDelete = 1
        };
        var service = CreateService(
            users: [user],
            userRoles: [userRole],
            associations: [association]);

        await service.UpsertUserAsync(new UpsertUserRequest(
            user.Id,
            "maintainer",
            "Maintainer",
            null,
            null,
            null,
            null,
            1,
            ["role-1"]));

        Assert.Equal(0, userRole.IsDelete);
        Assert.Equal(0, association.IsDelete);
    }

    [Fact]
    public async Task UpsertRoleAsync_SyncsGenericAssociationsAndLegacyRoleRelations()
    {
        var roles = new List<RoleEntity>();
        var roleMenus = new List<RoleMenuEntity>();
        var rolePermissions = new List<RolePermissionEntity>();
        var associations = new List<EntityAssociationEntity>();
        var service = CreateService(
            roles: roles,
            roleMenus: roleMenus,
            rolePermissions: rolePermissions,
            associations: associations);

        var role = await service.UpsertRoleAsync(new UpsertRoleRequest(
            null,
            "maintainer",
            "Maintainer",
            "Maintains system data",
            1,
            ["menu-1", "menu-2"],
            ["permission-1"]));

        Assert.Equal(["menu-1", "menu-2"], role.MenuIds.OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(["permission-1"], role.PermissionIds);
        Assert.Equal(
            ["menu-1", "menu-2"],
            associations
                .Where(entity => entity.SourceEntityId == role.Id && entity.AssociationType == SecurityAssociationTypes.RoleMenu && entity.IsDelete == 0)
                .Select(entity => entity.TargetEntityId)
                .OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(
            ["permission-1"],
            associations
                .Where(entity => entity.SourceEntityId == role.Id && entity.AssociationType == SecurityAssociationTypes.RolePermission && entity.IsDelete == 0)
                .Select(entity => entity.TargetEntityId));
        Assert.Equal(
            ["menu-1", "menu-2"],
            roleMenus
                .Where(entity => entity.RoleId == role.Id && entity.IsDelete == 0)
                .Select(entity => entity.MenuId)
                .OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(
            ["permission-1"],
            rolePermissions
                .Where(entity => entity.RoleId == role.Id && entity.IsDelete == 0)
                .Select(entity => entity.PermissionId));
    }

    [Fact]
    public async Task UpsertPermissionAsync_RequiresExistingMenu()
    {
        var service = CreateService();

        var missingMenu = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpsertPermissionAsync(new UpsertPermissionRequest(null, "System:User:Create", "Create user", "menu-missing")));

        Assert.Equal("Permission menu does not exist.", missingMenu.Message);

        var missingMenuId = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpsertPermissionAsync(new UpsertPermissionRequest(null, "System:User:Create", "Create user", null)));

        Assert.Equal("Permission menu is required.", missingMenuId.Message);
    }

    [Fact]
    public async Task UpsertPermissionAsync_SavesButtonPermissionUnderMenu()
    {
        var menu = new MenuEntity { Id = "menu-users", Name = "SystemUsers", Path = "/system/users" };
        var permissions = new List<PermissionEntity>();
        var service = CreateService(menus: [menu], permissions: permissions);

        var permission = await service.UpsertPermissionAsync(new UpsertPermissionRequest(
            null,
            "System:User:Create",
            "Create user",
            menu.Id));

        Assert.Equal(menu.Id, permission.MenuId);
        Assert.Equal(["System:User:Create"], permissions.Select(entity => entity.Code));
        Assert.Equal([menu.Id], permissions.Select(entity => entity.MenuId));
    }

    [Fact]
    public async Task DeleteMenuAsync_SoftDeletesAttachedButtonPermissions()
    {
        var menu = new MenuEntity { Id = "menu-users", Name = "SystemUsers", Path = "/system/users" };
        var attached = new PermissionEntity
        {
            Id = "permission-create",
            Code = "System:User:Create",
            Name = "Create user",
            MenuId = menu.Id
        };
        var detached = new PermissionEntity
        {
            Id = "permission-role",
            Code = "System:Role:Create",
            Name = "Create role",
            MenuId = "menu-roles"
        };
        var service = CreateService(menus: [menu], permissions: [attached, detached]);

        var deleted = await service.DeleteMenuAsync(menu.Id);

        Assert.True(deleted);
        Assert.Equal(1, menu.IsDelete);
        Assert.Equal(1, attached.IsDelete);
        Assert.Equal(0, detached.IsDelete);
    }

    [Fact]
    public async Task UpsertUserGroupAsync_RejectsDuplicateCodesBeforeDatabaseConstraint()
    {
        var service = CreateService(userGroups:
        [
            new UserGroupEntity { Id = "group-1", Code = "ops", Name = "Operations" }
        ]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpsertUserGroupAsync(new UpsertUserGroupRequest(null, "OPS", "Duplicate", null, 1)));

        Assert.Equal("Code already exists.", ex.Message);
    }

    private static SystemManagementService CreateService(
        IList<UserEntity>? users = null,
        IList<RoleEntity>? roles = null,
        IList<MenuEntity>? menus = null,
        IList<PermissionEntity>? permissions = null,
        IList<UserGroupEntity>? userGroups = null,
        IList<RoleGroupEntity>? roleGroups = null,
        IList<DepartmentEntity>? departments = null,
        IList<AssignmentEntity>? assignments = null,
        IList<UserRoleEntity>? userRoles = null,
        IList<RoleMenuEntity>? roleMenus = null,
        IList<RolePermissionEntity>? rolePermissions = null,
        IList<EntityAssociationEntity>? associations = null)
    {
        return new SystemManagementService(
            new InMemoryUserDomain(users ?? []),
            new InMemoryRoleDomain(roles ?? []),
            new InMemoryMenuDomain(menus ?? []),
            new InMemoryPermissionDomain(permissions ?? []),
            new InMemoryUserGroupDomain(userGroups ?? []),
            new InMemoryRoleGroupDomain(roleGroups ?? []),
            new InMemoryDepartmentDomain(departments ?? []),
            new InMemoryAssignmentDomain(assignments ?? []),
            new InMemoryUserRoleDomain(userRoles ?? []),
            new InMemoryRoleMenuDomain(roleMenus ?? []),
            new InMemoryRolePermissionDomain(rolePermissions ?? []),
            new InMemoryEntityAssociationDomain(associations ?? []));
    }
}

internal sealed class InMemoryUserGroupDomain(IList<UserGroupEntity> entities)
    : InMemorySecurityDomain<UserGroupEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IUserGroupDomain;

internal sealed class InMemoryRoleGroupDomain(IList<RoleGroupEntity> entities)
    : InMemorySecurityDomain<RoleGroupEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IRoleGroupDomain;

internal sealed class InMemoryDepartmentDomain(IList<DepartmentEntity> entities)
    : InMemorySecurityDomain<DepartmentEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IDepartmentDomain;

internal sealed class InMemoryAssignmentDomain(IList<AssignmentEntity> entities)
    : InMemorySecurityDomain<AssignmentEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IAssignmentDomain;
