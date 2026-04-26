using System.Security.Cryptography;
using System.Text;

namespace TripleSpaceTranslator.Core.Services;

internal static class TencentCloudApiSigner
{
    private const string Algorithm = "TC3-HMAC-SHA256";

    public static string CreateAuthorization(
        string secretId,
        string secretKey,
        string service,
        string host,
        string action,
        string payload,
        long timestamp,
        out string credentialScope)
    {
        var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd");
        var canonicalHeaders =
            $"content-type:application/json; charset=utf-8\nhost:{host}\nx-tc-action:{action.ToLowerInvariant()}\n";
        const string signedHeaders = "content-type;host;x-tc-action";
        var hashedPayload = Sha256Hex(payload);
        var canonicalRequest =
            $"POST\n/\n\n{canonicalHeaders}\n{signedHeaders}\n{hashedPayload}";

        credentialScope = $"{date}/{service}/tc3_request";
        var stringToSign =
            $"{Algorithm}\n{timestamp}\n{credentialScope}\n{Sha256Hex(canonicalRequest)}";

        var secretDate = HmacSha256(Encoding.UTF8.GetBytes($"TC3{secretKey}"), date);
        var secretService = HmacSha256(secretDate, service);
        var secretSigning = HmacSha256(secretService, "tc3_request");
        var signature = HexEncode(HmacSha256(secretSigning, stringToSign));

        return $"{Algorithm} Credential={secretId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
    }

    private static string Sha256Hex(string content)
    {
        return HexEncode(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }

    private static byte[] HmacSha256(byte[] key, string message)
    {
        return HmacSha256(key, Encoding.UTF8.GetBytes(message));
    }

    private static byte[] HmacSha256(byte[] key, byte[] message)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(message);
    }

    private static string HexEncode(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }
}
