using System.Security.Cryptography;

namespace ReliableWebhookDeliveryHub.Application.Security;

public sealed class ApiKeyService
{
    private const string Prefix = "tnt_";

    public GeneratedApiKey Generate(Guid tenantId)
    {
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secretBase64Url = Base64UrlEncode(secretBytes);
        var apiKeyHash = SHA256.HashData(secretBytes);
        var apiKey = $"{Prefix}{tenantId:N}.{secretBase64Url}";

        return new GeneratedApiKey(apiKey, apiKeyHash);
    }

    public bool TryParse(string apiKey, out ParsedApiKey parsed)
    {
        parsed = default;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        var parts = apiKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        var tenantPart = parts[0];
        if (!tenantPart.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var tenantIdPart = tenantPart[Prefix.Length..];
        if (!Guid.TryParseExact(tenantIdPart, "N", out var tenantId))
        {
            return false;
        }

        if (!TryBase64UrlDecode(parts[1], out var secretBytes) || secretBytes.Length != 32)
        {
            return false;
        }

        parsed = new ParsedApiKey(tenantId, secretBytes);
        return true;
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool TryBase64UrlDecode(string input, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var padded = input.Replace('-', '+').Replace('_', '/');
        var paddingNeeded = padded.Length % 4;
        if (paddingNeeded == 1)
        {
            return false;
        }

        if (paddingNeeded > 0)
        {
            padded = padded.PadRight(padded.Length + (4 - paddingNeeded), '=');
        }

        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public readonly record struct GeneratedApiKey(string ApiKey, byte[] ApiKeyHash);

public readonly record struct ParsedApiKey(Guid TenantId, byte[] SecretBytes);
