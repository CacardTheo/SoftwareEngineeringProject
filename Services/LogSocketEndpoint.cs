namespace EasySaveWpf.Services;

internal static class LogSocketEndpoint
{
    /// <summary>
    /// Accepts <c>host:port</c> or a legacy <c>http(s)://host:port/...</c> URL from older settings.
    /// </summary>
    public static bool TryParse(string? raw, out string host, out int port)
    {
        host = "127.0.0.1";
        port = 5132;

        if (string.IsNullOrWhiteSpace(raw))
            return true;

        string s = raw.Trim();

        if (Uri.TryCreate(s, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            host = string.IsNullOrEmpty(uri.Host) ? "127.0.0.1" : uri.Host;
            port = uri.IsDefaultPort ? 5132 : uri.Port;
            return true;
        }

        int colon = s.LastIndexOf(':');
        if (colon > 0 && int.TryParse(s.AsSpan(colon + 1), out int p) && p > 0 && p <= 65535)
        {
            host = s[..colon].Trim();
            if (host.Length == 0)
                return false;
            port = p;
            return true;
        }

        host = s;
        port = 5132;
        return host.Length > 0;
    }
}
