using System.Security.Cryptography;

namespace LocalStack.Lambda.UrlShortener;

public static class SlugGenerator
{
    /// <summary>
    /// Generates a random, lowercase, URL‑safe slug of length six.
    /// Collision probability ~1/16 M per attempt (32‑bit space truncated to 6 chars).
    /// </summary>
    public static string Create()
    {
        Span<byte> buffer = stackalloc byte[4];
        RandomNumberGenerator.Fill(buffer);

        var base64 = Convert.ToBase64String(buffer);
        return base64.Replace('+', '-').Replace('/', '_')[..6].ToLowerInvariant();
    }
}
