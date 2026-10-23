using System.Text;

namespace SmartInventory.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;

    public bool HasValidSigningKey() =>
        !string.IsNullOrWhiteSpace(SigningKey) && Encoding.UTF8.GetByteCount(SigningKey) >= 32;
}
