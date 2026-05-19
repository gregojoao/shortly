namespace Shortly.Domain;

public static class ShortlyUrlValidator
{
    public const int MaxUrlLength = 2048;

    public static bool IsAcceptable(
        Uri? url,
        IReadOnlyCollection<string> allowedSchemes,
        IReadOnlyCollection<string> allowedHosts)
    {
        ArgumentNullException.ThrowIfNull(allowedSchemes);
        ArgumentNullException.ThrowIfNull(allowedHosts);

        if (url is null || !url.IsAbsoluteUri)
        {
            return false;
        }

        if (url.OriginalString.Length > MaxUrlLength)
        {
            return false;
        }

        if (!HasAllowedScheme(url, allowedSchemes))
        {
            return false;
        }

        return allowedHosts.Count == 0 || HostMatches(url.Host, allowedHosts);
    }

    private static bool HasAllowedScheme(Uri url, IReadOnlyCollection<string> allowedSchemes)
    {
        foreach (var scheme in allowedSchemes)
        {
            if (string.Equals(url.Scheme, scheme, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HostMatches(string host, IReadOnlyCollection<string> allowedHosts)
    {
        foreach (var allowed in allowedHosts)
        {
            if (string.IsNullOrWhiteSpace(allowed))
            {
                continue;
            }

            if (string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (allowed.StartsWith("*.", StringComparison.Ordinal))
            {
                var suffix = allowed[1..];
                if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                    host.Length > suffix.Length)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
