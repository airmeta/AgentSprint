using AgentSprint.Model.Modules.Security;
using AgentSprint.Service.Impls.SecurityServices;

namespace AgentSprint.Tests;

public sealed class SecurityAuthorizationServiceTests
{
    [Fact]
    public async Task ResolveMenusAndPermissions_ReturnEmptyWhenUserHasNoRoles()
    {
        var service = CreateService();

        var menus = await service.ResolveMenusAsync("user-without-role");
        var permissions = await service.ResolvePermissionCodesAsync("user-without-role");
        var roles = await service.ResolveRoleCodesAsync("user-without-role");

        Assert.Empty(roles);
        Assert.Empty(menus);
        Assert.Empty(permissions);
    }

    [Fact]
    public void ResolveRoleIdsFromAssociations_IncludesUserGroupGrantedRoles()
    {
        var roleIds = SecurityAuthorizationService.ResolveRoleIdsFromAssociations(
            "user-1",
            [
                new EntityAssociationEntity
                {
                    SourceEntityId = "user-1",
                    TargetEntityId = "group-1",
                    AssociationType = SecurityAssociationTypes.UserUserGroup
                },
                new EntityAssociationEntity
                {
                    SourceEntityId = "group-1",
                    TargetEntityId = "role-1",
                    AssociationType = SecurityAssociationTypes.UserGroupRole
                }
            ]);

        var roleId = Assert.Single(roleIds);
        Assert.Equal("role-1", roleId);
    }

    [Fact]
    public void ResolveRoleIdsFromAssociations_IncludesRolesContainedByRoleGroup()
    {
        var roleIds = SecurityAuthorizationService.ResolveRoleIdsFromAssociations(
            "user-1",
            [
                new EntityAssociationEntity
                {
                    SourceEntityId = "user-1",
                    TargetEntityId = "role-group-1",
                    AssociationType = SecurityAssociationTypes.UserRoleGroup
                },
                new EntityAssociationEntity
                {
                    SourceEntityId = "role-1",
                    TargetEntityId = "role-group-1",
                    AssociationType = SecurityAssociationTypes.RoleRoleGroup
                }
            ]);

        var roleId = Assert.Single(roleIds);
        Assert.Equal("role-1", roleId);
    }

    [Fact]
    public void ResolveRoleIdsFromAssociations_IncludesUserGroupGrantedRoleGroupRoles()
    {
        var roleIds = SecurityAuthorizationService.ResolveRoleIdsFromAssociations(
            "user-1",
            [
                new EntityAssociationEntity
                {
                    SourceEntityId = "user-1",
                    TargetEntityId = "group-1",
                    AssociationType = SecurityAssociationTypes.UserUserGroup
                },
                new EntityAssociationEntity
                {
                    SourceEntityId = "group-1",
                    TargetEntityId = "role-group-1",
                    AssociationType = SecurityAssociationTypes.UserGroupRoleGroup
                },
                new EntityAssociationEntity
                {
                    SourceEntityId = "role-1",
                    TargetEntityId = "role-group-1",
                    AssociationType = SecurityAssociationTypes.RoleRoleGroup
                }
            ]);

        var roleId = Assert.Single(roleIds);
        Assert.Equal("role-1", roleId);
    }

    private static SecurityAuthorizationService CreateService(
        IList<RoleEntity>? roles = null,
        IList<MenuEntity>? menus = null,
        IList<PermissionEntity>? permissions = null,
        IList<UserRoleEntity>? userRoles = null,
        IList<RoleMenuEntity>? roleMenus = null,
        IList<RolePermissionEntity>? rolePermissions = null,
        IList<EntityAssociationEntity>? associations = null)
    {
        return new SecurityAuthorizationService(
            new InMemoryRoleDomain(roles ?? []),
            new InMemoryMenuDomain(menus ?? []),
            new InMemoryPermissionDomain(permissions ?? []),
            new InMemoryUserRoleDomain(userRoles ?? []),
            new InMemoryRoleMenuDomain(roleMenus ?? []),
            new InMemoryRolePermissionDomain(rolePermissions ?? []),
            new InMemoryEntityAssociationDomain(associations ?? []));
    }
}
