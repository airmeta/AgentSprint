using AgentSprint.Model.Modules.Security;
using AgentSprint.Service.Impls.SecurityServices;

namespace AgentSprint.Tests;

public sealed class SecurityAuthorizationServiceTests
{
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
}
