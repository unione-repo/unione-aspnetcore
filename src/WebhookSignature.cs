using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace UniOne.Wrapper;

/// <summary>
/// Signature of the webhook notifications: the "auth" field is the MD5 of the request body in which the "auth"
/// value is replaced by the API key of the user/project of the webhook.
/// </summary>
public static class WebhookSignature
{
    private static readonly Regex Auth = new("\"auth\"\\s*:\\s*\"([0-9a-fA-F]{32})\"", RegexOptions.Compiled);

    /// <summary>Placeholder of <see cref="Sign"/>.</summary>
    public const string Placeholder = "00000000000000000000000000000000";

    /// <summary>Whether the body is signed with the API key.</summary>
    public static bool Verify(string body, string apiKey)
    {
        var match = Auth.Match(body);
        if (apiKey.Length == 0 || !match.Success)
        {
            return false;
        }
        var auth = match.Groups[1].Value;
        var expected = Md5(body.Replace(auth, apiKey, StringComparison.Ordinal));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(auth.ToLowerInvariant()), Encoding.ASCII.GetBytes(expected));
    }

    /// <summary>Signs a body whose "auth" is <see cref="Placeholder"/>, e.g. for tests.</summary>
    public static string Sign(string body, string apiKey)
    {
        return body.Replace(Placeholder, Md5(body.Replace(Placeholder, apiKey, StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    private static string Md5(string text)
    {
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}
