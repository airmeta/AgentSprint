using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Service.Security;
using AgentSprint.Service.Services;
using AgentSprint.Service.Services.AuthServices;
using AgentSprint.Service.Services.SecurityServices;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AgentSprint.Service.Impls.AuthServices;

public sealed class AuthService : AgentSprintServiceBase, IAuthService
{
    private readonly IUserDomain _userDomain;
    private readonly ISecurityAuthorizationService _authorizationService;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        IUserDomain userDomain,
        ISecurityAuthorizationService authorizationService,
        IOptions<JwtOptions> jwtOptions)
    {
        _userDomain = userDomain;
        _authorizationService = authorizationService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("Username and password are required.");
        }

        var user = await _userDomain.FindByUsernameAsync(request.Username);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Username or password is incorrect.");
        }

        var roles = await _authorizationService.ResolveRoleCodesAsync(user.Id);
        var token = CreateAccessToken(user, roles);
        return new LoginResult(token, user.Id, user.Username, user.DisplayName, user.Avatar, roles);
    }

    public async Task<IReadOnlyList<string>> GetAccessCodesAsync(string userId)
    {
        return await _authorizationService.ResolvePermissionCodesAsync(userId);
    }

    public string CreateAccessToken(string userId, string username, IReadOnlyList<string> roles)
    {
        var user = new UserEntity
        {
            Id = userId,
            Username = username,
            DisplayName = username
        };

        return CreateAccessToken(user, roles);
    }

    private string CreateAccessToken(UserEntity user, IReadOnlyList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
