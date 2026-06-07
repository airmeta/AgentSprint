namespace AgentSprint.Model.Modules.Security.Dtos;

public sealed record LoginRequest(string? Username, string? Password);

public sealed record LoginResult(
    string AccessToken,
    string Id,
    string Username,
    string RealName,
    string? Avatar,
    IReadOnlyList<string> Roles,
    string HomePath = "/dashboard/workspace");

public sealed record CurrentUserResult(
    string UserId,
    string Id,
    string Username,
    string RealName,
    string? Avatar,
    IReadOnlyList<string> Roles,
    string HomePath,
    string Desc,
    string Token);

public sealed record UserOptionResult(
    string Id,
    string Username,
    string DisplayName);
