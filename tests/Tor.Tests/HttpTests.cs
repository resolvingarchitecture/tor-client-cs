using System.Text;
using Ra.Common;
using Xunit;
using static Ra.Tor.Http;

namespace Ra.Tor.Tests;

public class HttpTests
{
    private static byte[] Bytes(string s) => Encoding.ASCII.GetBytes(s);

    [Fact]
    public void ParseUrlWithPathAndPort()
    {
        var p = ParseUrl("http://example.onion:81/path");
        Assert.Equal("example.onion", p.Host);
        Assert.Equal(81, p.Port);
        Assert.Equal("/path", p.Path);
    }

    [Fact]
    public void ParseUrlWithNoPathDefaultsToRoot()
    {
        var p = ParseUrl("http://example.onion");
        Assert.Equal("example.onion", p.Host);
        Assert.Equal(80, p.Port);
        Assert.Equal("/", p.Path);
    }

    [Fact]
    public void ParseUrlRejectsHttps()
    {
        Assert.Throws<RaException>(() => ParseUrl("https://example.onion/"));
    }

    [Fact]
    public void ParseUrlRejectsBadPort()
    {
        Assert.Throws<RaException>(() => ParseUrl("http://example.onion:notaport/"));
    }

    [Fact]
    public void FormatGetHasConnectionClose()
    {
        var req = FormatGet("example.onion", "/path");
        Assert.StartsWith("GET /path HTTP/1.1\r\n", req);
        Assert.Contains("Host: example.onion\r\n", req);
        Assert.EndsWith("Connection: close\r\n\r\n", req);
    }

    [Fact]
    public void SplitBodyAfterHeaders()
    {
        var raw = Bytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello");
        Assert.Equal("hello", Encoding.ASCII.GetString(SplitBody(raw)));
    }

    [Fact]
    public void SplitBodyWithNoSeparatorReturnsWholeBuffer()
    {
        var raw = Bytes("not really http");
        Assert.Equal(raw, SplitBody(raw));
    }
}
