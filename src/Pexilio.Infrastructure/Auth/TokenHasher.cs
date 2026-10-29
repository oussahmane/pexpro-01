using System.Security.Cryptography;
using System.Text;

namespace Pexilio.Infrastructure.Auth;

public static class TokenHasher
{
    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    public static (RefreshToken Token, string Plaintext) Create(Guid userId, DateTimeOffset now, TimeSpan lifetime)
    {
        var plaintext = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var token = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = Hash(plaintext),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(lifetime),
        };
        return (token, plaintext);
    }
}
