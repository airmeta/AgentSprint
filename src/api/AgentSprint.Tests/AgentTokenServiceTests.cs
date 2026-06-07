using AgentSprint.Entry.Development;
using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Impls.SecurityServices;
using AgentSprint.Service.Security;

using Microsoft.Extensions.Options;

namespace AgentSprint.Tests;

public sealed class AgentTokenServiceTests
{
    [Fact]
    public async Task CreateTokenAsync_ReturnsFullTokenOnceAndStoresOnlyMetadata()
    {
        var tokenEntities = new List<AgentTokenEntity>();
        var service = CreateService(tokenEntities: tokenEntities);

        var result = await service.CreateTokenAsync(
            new CreateAgentTokenRequest("local-codex", DateTime.UtcNow.AddDays(7), null, null),
            "user-1",
            ["developer"]);

        Assert.Equal(64, result.Token.Length);
        Assert.Equal("local-codex", result.Metadata.Name);
        Assert.Equal("user-1", result.Metadata.OwnerUserId);
        Assert.Contains("...", result.Metadata.MaskedToken, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Token, result.Metadata.MaskedToken, StringComparison.Ordinal);
        Assert.Equal(result.Token, result.Metadata.Token);
        Assert.Single(tokenEntities);
        Assert.NotEqual(result.Token, tokenEntities[0].TokenHash);
        Assert.Equal(result.Token, tokenEntities[0].TokenValue);
        Assert.Equal(result.Token[..8], tokenEntities[0].TokenPrefix);
        Assert.Equal(result.Token[^8..], tokenEntities[0].TokenSuffix);
    }

    [Fact]
    public async Task CreateTokenAsync_AllowsDevelopmentLoginUserAsOwner()
    {
        var tokenEntities = new List<AgentTokenEntity>();
        var service = CreateService(
            userDomain: new DevelopmentUserDomain(),
            tokenEntities: tokenEntities);

        var result = await service.CreateTokenAsync(
            new CreateAgentTokenRequest("dev-token", DateTime.UtcNow.AddDays(7), null, null),
            "dev-admin",
            ["super"]);

        Assert.Equal("dev-admin", result.Metadata.OwnerUserId);
        Assert.Equal("admin", result.Metadata.OwnerUsername);
        Assert.Single(tokenEntities);
    }

    [Fact]
    public async Task ListTokensAsync_FiltersNormalUsersAndLetsSuperSeeAll()
    {
        var tokenEntities = new List<AgentTokenEntity>();
        var service = CreateService(tokenEntities: tokenEntities);
        await service.CreateTokenAsync(new CreateAgentTokenRequest("mine", DateTime.UtcNow.AddDays(7), null, null), "user-1", ["developer"]);
        await service.CreateTokenAsync(new CreateAgentTokenRequest("other", DateTime.UtcNow.AddDays(7), null, "user-2"), "admin-1", ["super"]);

        var normal = await service.ListTokensAsync("user-1", ["developer"]);
        var super = await service.ListTokensAsync("admin-1", ["super"]);

        Assert.Single(normal);
        Assert.Equal("mine", normal[0].Name);
        Assert.Equal(2, super.Count);
    }

    [Fact]
    public async Task ValidateForMcpAsync_RejectsExpiredToken()
    {
        var tokenEntities = new List<AgentTokenEntity>();
        var service = CreateService(tokenEntities: tokenEntities);
        var result = await service.CreateTokenAsync(
            new CreateAgentTokenRequest("expired", DateTime.UtcNow.AddDays(1), null, null),
            "user-1",
            ["developer"]);
        tokenEntities[0].ExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ValidateForMcpAsync(new AgentTokenValidationRequest(result.Token)));
    }

    [Fact]
    public async Task ValidateForMcpAsync_ReturnsJwtForTokenOwner()
    {
        var tokenEntities = new List<AgentTokenEntity>();
        var service = CreateService(tokenEntities: tokenEntities);
        var result = await service.CreateTokenAsync(
            new CreateAgentTokenRequest("mcp", DateTime.UtcNow.AddDays(1), "project-1", null),
            "user-1",
            ["developer"]);

        var validation = await service.ValidateForMcpAsync(new AgentTokenValidationRequest(result.Token));

        Assert.False(string.IsNullOrWhiteSpace(validation.AccessToken));
        Assert.Equal("user-1", validation.UserId);
        Assert.Equal("project-1", validation.ProjectId);
        Assert.NotNull(tokenEntities[0].LastUsedAt);
    }

    private static AgentTokenService CreateService(
        IUserDomain? userDomain = null,
        IList<AgentTokenEntity>? tokenEntities = null)
    {
        var users = new List<UserEntity>
        {
            new() { Id = "user-1", Username = "developer", DisplayName = "Developer", Status = 1 },
            new() { Id = "user-2", Username = "tester", DisplayName = "Tester", Status = 1 },
            new() { Id = "admin-1", Username = "admin", DisplayName = "Admin", Status = 1 }
        };
        var roles = new List<RoleEntity>
        {
            new() { Id = "role-dev", Code = "developer", Name = "Developer", Status = 1 },
            new() { Id = "role-super", Code = "super", Name = "Super", Status = 1 }
        };
        var associations = new List<EntityAssociationEntity>
        {
            new() { SourceEntityId = "user-1", TargetEntityId = "role-dev", AssociationType = SecurityAssociationTypes.UserRole },
            new() { SourceEntityId = "admin-1", TargetEntityId = "role-super", AssociationType = SecurityAssociationTypes.UserRole }
        };
        userDomain ??= new InMemoryUserDomain(users);
        var authorization = new SecurityAuthorizationService(
            new InMemoryRoleDomain(roles),
            new InMemoryMenuDomain([]),
            new InMemoryPermissionDomain([]),
            new InMemoryUserRoleDomain([]),
            new InMemoryRoleMenuDomain([]),
            new InMemoryRolePermissionDomain([]),
            new InMemoryEntityAssociationDomain(associations));
        var auth = new DevelopmentAuthService(Options.Create(new JwtOptions
        {
            SigningKey = "AgentSprintTestsSigningKeyMustBeLongEnough",
            Issuer = "AgentSprint.Tests",
            Audience = "AgentSprint.Tests",
            AccessTokenMinutes = 10
        }));

        return new AgentTokenService(
            new InMemoryAgentTokenDomain(tokenEntities ?? []),
            userDomain,
            authorization,
            auth);
    }
}

internal sealed class InMemoryAgentTokenDomain(IList<AgentTokenEntity> entities)
    : InMemorySecurityDomain<AgentTokenEntity>(entities), IAgentTokenDomain
{
    public Task<AgentTokenEntity?> FindByTokenHashAsync(string tokenHash)
    {
        return Task.FromResult(Entities.SingleOrDefault(entity =>
            entity.TokenHash == tokenHash &&
            entity.IsDelete == 0));
    }
}
