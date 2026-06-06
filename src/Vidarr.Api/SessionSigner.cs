using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vidarr.Contracts.Abstractions;

namespace Vidarr.Api;

public interface ISessionSigner
{
    string Sign(string secret, string subject, TimeSpan ttl);
    bool TryVerify(string secret, string token, out string subject);
}

public sealed class HmacSessionSigner : ISessionSigner
{
    private readonly ISystemClock _clock;
    public HmacSessionSigner(ISystemClock clock) { _clock = clock; }

    public string Sign(string secret, string subject, TimeSpan ttl)
    {
        var payload = JsonSerializer.Serialize(new SessionPayload(subject, _clock.UtcNow.Add(ttl).ToUnixTimeSeconds()));
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(DecodeSecret(secret));
        var sig = hmac.ComputeHash(payloadBytes);
        return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(sig)}";
    }

    public bool TryVerify(string secret, string token, out string subject)
    {
        subject = string.Empty;
        if (string.IsNullOrEmpty(token)) return false;
        var dot = token.IndexOf('.');
        if (dot <= 0 || dot >= token.Length - 1) return false;
        byte[] payloadBytes;
        byte[] sigBytes;
        try
        {
            payloadBytes = Base64UrlDecode(token[..dot]);
            sigBytes = Base64UrlDecode(token[(dot + 1)..]);
        }
        catch (FormatException)
        {
            return false;
        }
        using var hmac = new HMACSHA256(DecodeSecret(secret));
        var expected = hmac.ComputeHash(payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(expected, sigBytes)) return false;
        SessionPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SessionPayload>(payloadBytes);
        }
        catch (JsonException)
        {
            return false;
        }
        if (payload is null || string.IsNullOrEmpty(payload.Sub)) return false;
        if (DateTimeOffset.FromUnixTimeSeconds(payload.Exp) < _clock.UtcNow) return false;
        subject = payload.Sub;
        return true;
    }

    private static byte[] DecodeSecret(string secret)
    {
        try { return Convert.FromBase64String(secret); }
        catch (FormatException) { return Encoding.UTF8.GetBytes(secret); }
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
            case 1: throw new FormatException("Invalid base64url length");
        }
        return Convert.FromBase64String(padded);
    }

    private sealed record SessionPayload(string Sub, long Exp);
}
