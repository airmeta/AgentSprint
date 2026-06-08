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

    [Fact]
    public async Task UpsertDictionaryItemAsync_RequiresExistingDictionaryType()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpsertDictionaryItemAsync(new UpsertDictionaryItemRequest(
                null,
                "dict-missing",
                "enabled",
                "Enabled",
                null,
                10,
                1)));

        Assert.Equal("Dictionary type does not exist.", ex.Message);
    }

    [Fact]
    public async Task UpsertDictionaryItemAsync_RejectsDuplicateCodeWithinType()
    {
        var type = new DictionaryTypeEntity { Id = "dict-type-status", Code = "status", Name = "Status" };
        var service = CreateService(
            dictionaryTypes: [type],
            dictionaryItems:
            [
                new DictionaryItemEntity
                {
                    Id = "dict-item-enabled",
                    DictionaryTypeId = type.Id,
                    Code = "enabled",
                    Name = "Enabled"
                }
            ]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpsertDictionaryItemAsync(new UpsertDictionaryItemRequest(
                null,
                type.Id,
                "ENABLED",
                "Duplicate enabled",
                null,
                20,
                1)));

        Assert.Equal("Dictionary item code already exists.", ex.Message);
    }

    [Fact]
    public async Task DeleteDictionaryTypeAsync_SoftDeletesChildItems()
    {
        var type = new DictionaryTypeEntity { Id = "dict-type-status", Code = "status", Name = "Status" };
        var child = new DictionaryItemEntity
        {
            Id = "dict-item-enabled",
            DictionaryTypeId = type.Id,
            Code = "enabled",
            Name = "Enabled"
        };
        var sibling = new DictionaryItemEntity
        {
            Id = "dict-item-priority-high",
            DictionaryTypeId = "dict-type-priority",
            Code = "high",
            Name = "High"
        };
        var service = CreateService(dictionaryTypes: [type], dictionaryItems: [child, sibling]);

        var deleted = await service.DeleteDictionaryTypeAsync(type.Id);

        Assert.True(deleted);
        Assert.Equal(1, type.IsDelete);
        Assert.Equal(1, child.IsDelete);
        Assert.Equal(0, sibling.IsDelete);
    }

    [Fact]
    public async Task UpsertRuntimeEnvironmentAsync_RejectsDuplicateCodeWithinProject()
    {
        var service = CreateService(runtimeEnvironments:
        [
            new RuntimeEnvironmentEntity { Id = "env-1", ProjectId = "project-1", Code = "test", Name = "Test" }
        ]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpsertRuntimeEnvironmentAsync(new UpsertRuntimeEnvironmentRequest(
                null,
                "project-1",
                null,
                null,
                "TEST",
                "Duplicate",
                "test",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                20,
                1)));

        Assert.Equal("Runtime environment code already exists in the project.", ex.Message);
    }

    [Fact]
    public async Task UpsertRuntimeEnvironmentContainerAsync_RequiresExistingEnvironmentAndUniqueContainerName()
    {
        var environment = new RuntimeEnvironmentEntity { Id = "env-1", Code = "test", Name = "Test" };
        var service = CreateService(
            runtimeEnvironments: [environment],
            runtimeEnvironmentContainers:
            [
                new RuntimeEnvironmentContainerEntity
                {
                    Id = "container-admin",
                    RuntimeEnvironmentId = environment.Id,
                    Name = "agentsprint-admin",
                    HostPort = 5999,
                    ContainerPort = 80
                }
            ]);

        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpsertRuntimeEnvironmentContainerAsync(new UpsertRuntimeEnvironmentContainerRequest(
                null,
                "missing-env",
                "agentsprint-api",
                5000,
                5000,
                "tcp",
                null,
                10,
                1)));
        Assert.Equal("Runtime environment does not exist.", missing.Message);

        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpsertRuntimeEnvironmentContainerAsync(new UpsertRuntimeEnvironmentContainerRequest(
                null,
                environment.Id,
                "AGENTSPRINT-ADMIN",
                5999,
                80,
                "tcp",
                null,
                10,
                1)));
        Assert.Equal("Container name already exists in the runtime environment.", duplicate.Message);
    }

    [Fact]
    public async Task DeleteRuntimeEnvironmentAsync_SoftDeletesChildContainers()
    {
        var environment = new RuntimeEnvironmentEntity { Id = "env-1", Code = "test", Name = "Test" };
        var child = new RuntimeEnvironmentContainerEntity
        {
            Id = "container-admin",
            RuntimeEnvironmentId = environment.Id,
            Name = "agentsprint-admin",
            HostPort = 5999,
            ContainerPort = 80
        };
        var sibling = new RuntimeEnvironmentContainerEntity
        {
            Id = "container-other",
            RuntimeEnvironmentId = "env-other",
            Name = "agentsprint-api",
            HostPort = 5000,
            ContainerPort = 5000
        };
        var service = CreateService(runtimeEnvironments: [environment], runtimeEnvironmentContainers: [child, sibling]);

        var deleted = await service.DeleteRuntimeEnvironmentAsync(environment.Id);

        Assert.True(deleted);
        Assert.Equal(1, environment.IsDelete);
        Assert.Equal(1, child.IsDelete);
        Assert.Equal(0, sibling.IsDelete);
    }

    [Fact]
    public async Task UpsertPromptTemplateAsync_RejectsUnsupportedAgentEnvironment()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpsertPromptTemplateAsync(new UpsertPromptTemplateRequest(
                null,
                "claude_code",
                "task_execution",
                "Task execution",
                "Prompt content",
                null,
                10,
                1)));

        Assert.Equal("Only Codex prompt templates are supported currently.", ex.Message);
    }

    [Fact]
    public async Task UpsertPromptTemplateAsync_RejectsDuplicateCodeWithinEnvironment()
    {
        var service = CreateService(promptTemplates:
        [
            new PromptTemplateEntity
            {
                Id = "prompt-1",
                AgentEnvironment = "codex",
                Code = "task_execution",
                Name = "Task execution",
                Content = "Existing prompt"
            }
        ]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpsertPromptTemplateAsync(new UpsertPromptTemplateRequest(
                null,
                "codex",
                "TASK_EXECUTION",
                "Duplicate",
                "Prompt content",
                null,
                20,
                1)));

        Assert.Equal("Prompt template code already exists in the agent environment.", ex.Message);
    }

    [Fact]
    public async Task UpsertPromptTemplateAsync_RejectsNonFixedPromptTemplateCode()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpsertPromptTemplateAsync(new UpsertPromptTemplateRequest(
                null,
                "codex",
                "custom_prompt",
                "Custom prompt",
                "Prompt content",
                null,
                30,
                1)));

        Assert.Equal("Only fixed Codex prompt templates can be maintained.", ex.Message);
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
        IList<DictionaryTypeEntity>? dictionaryTypes = null,
        IList<DictionaryItemEntity>? dictionaryItems = null,
        IList<RuntimeEnvironmentEntity>? runtimeEnvironments = null,
        IList<RuntimeEnvironmentContainerEntity>? runtimeEnvironmentContainers = null,
        IList<PromptTemplateEntity>? promptTemplates = null,
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
            new InMemoryDictionaryTypeDomain(dictionaryTypes ?? []),
            new InMemoryDictionaryItemDomain(dictionaryItems ?? []),
            new InMemoryRuntimeEnvironmentDomain(runtimeEnvironments ?? []),
            new InMemoryRuntimeEnvironmentContainerDomain(runtimeEnvironmentContainers ?? []),
            new InMemoryPromptTemplateDomain(promptTemplates ?? []),
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

internal sealed class InMemoryDictionaryTypeDomain(IList<DictionaryTypeEntity> entities)
    : InMemorySecurityDomain<DictionaryTypeEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IDictionaryTypeDomain;

internal sealed class InMemoryDictionaryItemDomain(IList<DictionaryItemEntity> entities)
    : InMemorySecurityDomain<DictionaryItemEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IDictionaryItemDomain;

internal sealed class InMemoryRuntimeEnvironmentDomain(IList<RuntimeEnvironmentEntity> entities)
    : InMemorySecurityDomain<RuntimeEnvironmentEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IRuntimeEnvironmentDomain;

internal sealed class InMemoryRuntimeEnvironmentContainerDomain(IList<RuntimeEnvironmentContainerEntity> entities)
    : InMemorySecurityDomain<RuntimeEnvironmentContainerEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IRuntimeEnvironmentContainerDomain;

internal sealed class InMemoryPromptTemplateDomain(IList<PromptTemplateEntity> entities)
    : InMemorySecurityDomain<PromptTemplateEntity>(entities), AgentSprint.Model.Modules.Security.Domains.IPromptTemplateDomain;
