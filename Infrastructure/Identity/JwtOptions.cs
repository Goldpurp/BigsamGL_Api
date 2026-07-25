namespace Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = "BigsamGL.Api";
    public string Audience { get; init; } = "BigsamGL.Web";
    public string Secret { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 30;
}
