namespace ReliableWebhookDeliveryHub.WebApi;

public static class DevDotEnv
{
    public static void LoadFromCurrentDirectoryIfPresent()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();

            if (value.Length > 1 &&
                ((value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)) ||
                 (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal))))
            {
                value = value[1..^1];
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                continue;
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
