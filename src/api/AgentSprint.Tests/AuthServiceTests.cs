using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Impls.AuthServices;
using AgentSprint.Service.Impls.SecurityServices;
using AgentSprint.Service.Security;
using AgentSprint.Service.Services.AuthServices;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AgentSprint.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_ReturnsTokenWhenUserHasRole()
    {
        var service = CreateService(
            users:
            [
                new UserEntity
                {
                    Id = "user-1",
                    Username = "admin",
                    DisplayName = "Admin",
                    PasswordHash = PasswordHasher.Hash("123456"),
                    Status = 1
                }
            ],
            associations:
            [
                new EntityAssociationEntity
                {
                    SourceEntityId = "user-1",
                    TargetEntityId = "role-super",
                    AssociationType = SecurityAssociationTypes.UserRole
                }
            ]);

        var result = await service.LoginAsync(new("admin", "123456", new("captcha-1", 120)));

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal(["super"], result.Roles);
    }

    [Fact]
    public async Task LoginAsync_RejectsUserWithoutRoles()
    {
        var service = CreateService(
            users:
            [
                new UserEntity
                {
                    Id = "user-without-role",
                    Username = "baoyl",
                    DisplayName = "Bao Yuanlong",
                    PasswordHash = PasswordHasher.Hash("123456"),
                    Status = 1
                }
            ]);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new("baoyl", "123456", new("captcha-1", 120))));

        Assert.Equal("No roles are assigned to this user.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_RejectsMissingOrInvalidCaptcha()
    {
        var service = CreateService(
            users:
            [
                new UserEntity
                {
                    Id = "user-1",
                    Username = "admin",
                    DisplayName = "Admin",
                    PasswordHash = PasswordHasher.Hash("123456"),
                    Status = 1
                }
            ],
            associations:
            [
                new EntityAssociationEntity
                {
                    SourceEntityId = "user-1",
                    TargetEntityId = "role-super",
                    AssociationType = SecurityAssociationTypes.UserRole
                }
            ],
            captchaService: new FailingCaptchaService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.LoginAsync(new("admin", "123456", new("captcha-1", 120))));

        Assert.Equal("Captcha verification failed.", ex.Message);
    }

    [Fact]
    public async Task CaptchaService_VerifiesChallengeOnceWithTolerance()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CaptchaService(memoryCache);

        var challenge = await service.CreateChallengeAsync();
        var expectedX = ReadExpectedCaptchaX(memoryCache, challenge.Id);

        Assert.Equal(320, challenge.Width);
        Assert.Equal(48, challenge.SliderWidth);
        var maxX = challenge.Width - challenge.SliderWidth;
        Assert.InRange(challenge.TargetX, (int)Math.Round(maxX * 0.4), (int)Math.Round(maxX * 0.95));
        Assert.True(await service.VerifyAsync(new(challenge.Id, expectedX + 10)));
        Assert.False(await service.VerifyAsync(new(challenge.Id, expectedX + 4)));
    }

    private static AuthService CreateService(
        IList<UserEntity> users,
        IList<EntityAssociationEntity>? associations = null,
        ICaptchaService? captchaService = null)
    {
        var roles = new List<RoleEntity>
        {
            new() { Id = "role-super", Code = "super", Name = "Super", Status = 1 }
        };
        var authorization = new SecurityAuthorizationService(
            new InMemoryRoleDomain(roles),
            new InMemoryMenuDomain([]),
            new InMemoryPermissionDomain([]),
            new InMemoryUserRoleDomain([]),
            new InMemoryRoleMenuDomain([]),
            new InMemoryRolePermissionDomain([]),
            new InMemoryEntityAssociationDomain(associations ?? []));

        return new AuthService(
            new InMemoryUserDomain(users),
            authorization,
            captchaService ?? new PassingCaptchaService(),
            Options.Create(new JwtOptions
            {
                SigningKey = "AgentSprintTestsSigningKeyMustBeLongEnough",
                Issuer = "AgentSprint.Tests",
                Audience = "AgentSprint.Tests",
                AccessTokenMinutes = 10
            }));
    }

    private static int ReadExpectedCaptchaX(IMemoryCache memoryCache, string captchaId)
    {
        Assert.True(memoryCache.TryGetValue<int>($"auth:captcha:{captchaId}", out var expectedX));
        return expectedX;
    }

    private sealed class PassingCaptchaService : ICaptchaService
    {
        public Task<CaptchaChallengeResult> CreateChallengeAsync()
        {
            return Task.FromResult(new CaptchaChallengeResult("captcha-1", 320, 48, 120));
        }

        public Task<bool> VerifyAsync(CaptchaVerifyRequest? request)
        {
            return Task.FromResult(request?.Id == "captcha-1" && request.X == 120);
        }
    }

    private sealed class FailingCaptchaService : ICaptchaService
    {
        public Task<CaptchaChallengeResult> CreateChallengeAsync()
        {
            return Task.FromResult(new CaptchaChallengeResult("captcha-1", 320, 48, 120));
        }

        public Task<bool> VerifyAsync(CaptchaVerifyRequest? request)
        {
            return Task.FromResult(false);
        }
    }
}
