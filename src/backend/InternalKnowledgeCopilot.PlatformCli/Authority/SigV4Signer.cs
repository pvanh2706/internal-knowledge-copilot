using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Amazon.Runtime;

namespace InternalKnowledgeCopilot.PlatformCli.Authority;

// AWS Signature Version 4 cho lời gọi API Gateway (authorization AWS_IAM). Ký host, x-amz-date (và x-amz-security-token
// của credential tạm thời) cùng hash payload; API Gateway xác minh chữ ký và điền danh tính caller vào requestContext.
public static class SigV4Signer
{
    public const string Algorithm = "AWS4-HMAC-SHA256";

    public static void Sign(HttpRequestMessage request, byte[] payload, ImmutableCredentials credentials, string region, string service, DateTimeOffset now)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("request_uri_required");
        var amzDate = now.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var date = amzDate[..8];
        var host = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";

        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = host,
            ["x-amz-date"] = amzDate,
        };
        if (credentials.UseToken)
        {
            headers["x-amz-security-token"] = credentials.Token;
        }

        var signedHeaders = string.Join(';', headers.Keys);
        var canonicalRequest = string.Join('\n',
            request.Method.Method,
            CanonicalPath(uri),
            CanonicalQuery(uri),
            string.Concat(headers.Select(header => $"{header.Key}:{header.Value.Trim()}\n")),
            signedHeaders,
            Hex(SHA256.HashData(payload)));
        var scope = $"{date}/{region}/{service}/aws4_request";
        var stringToSign = $"{Algorithm}\n{amzDate}\n{scope}\n{Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";

        var key = Hmac(Encoding.UTF8.GetBytes("AWS4" + credentials.SecretKey), date);
        key = Hmac(key, region);
        key = Hmac(key, service);
        key = Hmac(key, "aws4_request");
        var signature = Hex(Hmac(key, stringToSign));

        request.Headers.Host = host;
        request.Headers.Remove("X-Amz-Date");
        request.Headers.TryAddWithoutValidation("X-Amz-Date", amzDate);
        request.Headers.Remove("X-Amz-Security-Token");
        if (credentials.UseToken)
        {
            request.Headers.TryAddWithoutValidation("X-Amz-Security-Token", credentials.Token);
        }

        request.Headers.Remove("Authorization");
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            $"{Algorithm} Credential={credentials.AccessKey}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}");
    }

    // Mỗi segment được mã hóa theo RFC 3986 (API Gateway không phải S3: segment đã mã hóa được mã hóa lại).
    private static string CanonicalPath(Uri uri)
    {
        var path = uri.AbsolutePath;
        if (string.IsNullOrEmpty(path))
        {
            return "/";
        }

        return string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
    }

    private static string CanonicalQuery(Uri uri)
    {
        var query = uri.Query.TrimStart('?');
        if (query.Length == 0)
        {
            return string.Empty;
        }

        return string.Join('&', query.Split('&')
            .Select(pair => pair.Split('=', 2))
            .Select(parts => (Name: Uri.EscapeDataString(Uri.UnescapeDataString(parts[0])), Value: parts.Length == 2 ? Uri.EscapeDataString(Uri.UnescapeDataString(parts[1])) : string.Empty))
            .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
            .ThenBy(parameter => parameter.Value, StringComparer.Ordinal)
            .Select(parameter => $"{parameter.Name}={parameter.Value}"));
    }

    private static byte[] Hmac(byte[] key, string data) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
