using Ra.Common;

namespace Ra.Tor;

/// <summary>
/// A tiny HTTP/1.1 GET. HTTP only (no TLS) - HTTPS needs an
/// <see cref="System.Net.Security.SslStream"/> wrapped around the SOCKS
/// socket, see <c>TODO.md</c>. Ports <c>tor-client-rust</c>'s <c>http</c>
/// module.
/// </summary>
public static class Http
{
    private const string UserAgent = "ra-tor-client";

    public readonly record struct ParsedUrl(string Host, int Port, string Path);

    /// <summary>Splits <c>host</c>, <c>port</c> and <c>path</c> out of an
    /// <c>http://</c> URL. Throws on any other scheme.</summary>
    public static ParsedUrl ParseUrl(string url)
    {
        const string prefix = "http://";
        if (!url.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw RaException.Invalid("only http:// URLs are supported (HTTPS needs a TLS layer - see TODO.md)");
        }
        var rest = url[prefix.Length..];
        var slash = rest.IndexOf('/');
        var authority = slash == -1 ? rest : rest[..slash];
        var path = slash == -1 ? "/" : rest[slash..];

        var colon = authority.LastIndexOf(':');
        if (colon == -1)
        {
            return new ParsedUrl(authority, 80, path);
        }
        var host = authority[..colon];
        var portStr = authority[(colon + 1)..];
        if (!int.TryParse(portStr, out var port) || port is < 0 or > 65535)
        {
            throw RaException.Invalid("bad port");
        }
        return new ParsedUrl(host, port, path);
    }

    /// <summary>The GET request line + headers for <paramref name="path"/> on
    /// <paramref name="host"/>, <c>Connection: close</c>.</summary>
    public static string FormatGet(string host, string path) =>
        $"GET {path} HTTP/1.1\r\nHost: {host}\r\nUser-Agent: {UserAgent}\r\nAccept: */*\r\nConnection: close\r\n\r\n";

    /// <summary>Everything after the first CRLFCRLF (the body), or the whole
    /// buffer if no header/body separator is present.</summary>
    public static byte[] SplitBody(byte[] raw)
    {
        ReadOnlySpan<byte> sep = "\r\n\r\n"u8;
        var idx = raw.AsSpan().IndexOf(sep);
        return idx == -1 ? raw : raw[(idx + sep.Length)..];
    }

    /// <summary>Fetches <paramref name="url"/> (<c>http://</c> only) through
    /// the SOCKS5 proxy; returns the response body.</summary>
    public static byte[] FetchViaSocks(string proxyHost, int proxyPort, string url, TimeSpan timeout)
    {
        var parsed = ParseUrl(url);
        using var socket = Socks5.ConnectThrough(proxyHost, proxyPort, parsed.Host, parsed.Port, timeout);

        var req = System.Text.Encoding.ASCII.GetBytes(FormatGet(parsed.Host, parsed.Path));
        socket.Send(req);

        using var raw = new MemoryStream();
        var buffer = new byte[65536];
        int r;
        while ((r = socket.Receive(buffer)) > 0)
        {
            raw.Write(buffer, 0, r);
        }
        return SplitBody(raw.ToArray());
    }
}
