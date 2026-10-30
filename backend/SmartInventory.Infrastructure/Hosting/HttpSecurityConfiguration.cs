using Microsoft.Extensions.Configuration;

namespace SmartInventory.Infrastructure.Hosting;

/// <summary>Validates exact host/origin allowlists without accepting wildcard production defaults.</summary>
public static class HttpSecurityConfiguration
{
    private static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "::1"];

    public static string[] ApiHosts(IConfiguration configuration, bool local)
    {
        var value = configuration["AllowedHosts"];
        return ValidateHosts(value is null && local ? LoopbackHosts : value?.Split(';', StringSplitOptions.TrimEntries), "AllowedHosts");
    }

    public static string[] McpHosts(IConfiguration configuration, bool local) => ValidateHosts(
        configuration.GetSection("Mcp:AllowedHosts").Get<string[]>() ?? (local ? LoopbackHosts : null), "Mcp:AllowedHosts");

    public static string[] Origins(IConfiguration configuration, string sectionName, bool local, params string[] localDefaults)
    {
        var values = configuration.GetSection(sectionName).Get<string[]>() ?? (local ? localDefaults : null);
        if (values is not { Length: > 0 }) throw new InvalidOperationException($"Configure {sectionName} with explicit origins.");
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.EndsWith('/') ||
                !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                uri.Scheme != "https" && !(local && uri.Scheme == "http") ||
                uri.Host.Contains('*') || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidOperationException($"{sectionName} must contain exact HTTPS origins without credentials, paths, queries or fragments; local environments may use HTTP.");
        }
        return values.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static string NormalizeHost(string host) => host.Trim('[', ']').ToLowerInvariant();
    public static string HostFilterValue(IEnumerable<string> hosts) => string.Join(';', hosts.Select(host => host.Contains(':') ? "[" + host + "]" : host));

    private static string[] ValidateHosts(string[]? values, string name)
    {
        if (values is not { Length: > 0 }) throw new InvalidOperationException($"Configure {name} with explicit host names.");
        var hosts = new List<string>();
        foreach (var value in values)
        {
            var trimmed = value.Trim();
            if ((trimmed.Contains('[') || trimmed.Contains(']')) &&
                !(trimmed.StartsWith('[') && trimmed.EndsWith(']') &&
                  System.Net.IPAddress.TryParse(trimmed[1..^1], out var address) &&
                  address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6))
                throw new InvalidOperationException($"{name} accepts brackets only around an IPv6 address.");
            var host = NormalizeHost(trimmed);
            if (host.Length == 0 || host.Contains('*') || host.Contains('/') || host.Contains('@') ||
                Uri.CheckHostName(host) == UriHostNameType.Unknown)
                throw new InvalidOperationException($"{name} must contain exact DNS names or IP addresses without schemes, ports or wildcards.");
            hosts.Add(host);
        }
        return hosts.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
