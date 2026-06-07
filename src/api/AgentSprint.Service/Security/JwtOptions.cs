namespace AgentSprint.Service.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "AgentSprint";

    public string Audience { get; set; } = "AgentSprint.Admin";

    public string SigningKey { get; set; } = "AgentSprint-development-signing-key-change-me";

    public int AccessTokenMinutes { get; set; } = 120;
}

