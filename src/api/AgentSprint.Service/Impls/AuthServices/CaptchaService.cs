using System.Security.Cryptography;

using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Services.AuthServices;

using Microsoft.Extensions.Caching.Memory;

namespace AgentSprint.Service.Impls.AuthServices;

public sealed class CaptchaService : ICaptchaService
{
    private const int Width = 320;
    private const int SliderWidth = 48;
    private const int Tolerance = 12;
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(2);
    private readonly IMemoryCache _cache;

    public CaptchaService(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public Task<CaptchaChallengeResult> CreateChallengeAsync()
    {
        var id = Guid.NewGuid().ToString("N");
        var maxX = Width - SliderWidth;
        var minTargetX = (int)Math.Round(maxX * 0.4, MidpointRounding.AwayFromZero);
        var maxTargetX = (int)Math.Round(maxX * 0.95, MidpointRounding.AwayFromZero);
        var targetX = RandomNumberGenerator.GetInt32(minTargetX, maxTargetX + 1);

        _cache.Set(CacheKey(id), targetX, ChallengeLifetime);

        return Task.FromResult(new CaptchaChallengeResult(
            id,
            Width,
            SliderWidth,
            targetX));
    }

    /// <inheritdoc />
    public Task<bool> VerifyAsync(CaptchaVerifyRequest? request)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.Id) ||
            request.X is null ||
            !_cache.TryGetValue<int>(CacheKey(request.Id), out var expectedX))
        {
            return Task.FromResult(false);
        }

        _cache.Remove(CacheKey(request.Id));
        return Task.FromResult(Math.Abs(expectedX - request.X.Value) <= Tolerance);
    }

    private static string CacheKey(string id)
    {
        return $"auth:captcha:{id}";
    }
}
