using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Ra.Common;
using Xunit;

namespace Ra.Tor.Tests;

public class ClientTests
{
    private static (TcpListener listener, int port) Listen()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return (listener, ((IPEndPoint)listener.LocalEndpoint).Port);
    }

    /// <summary>Reads exactly <paramref name="n"/> bytes, or returns false if
    /// the connection closes first (a probe connection that never sends the
    /// SOCKS greeting).</summary>
    private static bool RecvExact(Socket socket, byte[] buf, int n)
    {
        var got = 0;
        while (got < n)
        {
            var r = socket.Receive(buf, got, n - got, SocketFlags.None);
            if (r <= 0) return false;
            got += r;
        }
        return true;
    }

    [Fact]
    public void StartFailsCleanlyWithoutADaemon()
    {
        var client = new TorClient(new Dictionary<string, string>
        {
            ["ra.tor.socksPort"] = "1",
            ["ra.tor.controlPort"] = "1",
        });
        Assert.False(client.Start());
        Assert.Equal(Status.Disconnected, client.GetStatus());
    }

    [Fact]
    public void SendWithoutStartReportsNotStarted()
    {
        var client = new TorClient(new Dictionary<string, string>
        {
            ["ra.tor.socksPort"] = "1",
            ["ra.tor.controlPort"] = "1",
        });
        var env = Envelope.HeadersOnly();
        env.SetHeader("url", "http://example.onion/");
        Assert.False(client.Send(env));
        Assert.Equal("Tor client not started", (string?)env.Header("error"));
    }

    [Fact]
    public void SendWithoutUrlHeaderErrors()
    {
        var client = new TorClient();
        var env = Envelope.HeadersOnly();
        Assert.False(client.Send(env));
        Assert.Equal("no url header", (string?)env.Header("error"));
    }

    [Fact]
    public void SocksConnectAndHttpFetchThroughAFakeProxy()
    {
        // A fake SOCKS5 proxy that also serves the "destination" HTTP
        // response, mirroring the other ports' integration test of the same
        // shape.
        var (controlListener, controlPort) = Listen();
        var controlThread = new Thread(() =>
        {
            using var c = controlListener.AcceptSocket();
            c.Close();
        });
        controlThread.Start();

        var (socksListener, socksPort) = Listen();
        var socksThread = new Thread(() =>
        {
            // The detector probes the SOCKS port before the real request, so
            // accept in a loop and handle whichever connection completes a
            // full handshake; a probe connection closes right after
            // connecting.
            while (true)
            {
                var c = socksListener.AcceptSocket();
                var greeting = new byte[3];
                if (!RecvExact(c, greeting, 3))
                {
                    c.Close();
                    continue;
                }
                c.Send(new byte[] { 0x05, 0x00 });

                var head = new byte[5];
                RecvExact(c, head, 5);
                var nameLen = head[4];
                var rest = new byte[nameLen + 2];
                RecvExact(c, rest, rest.Length);
                c.Send(new byte[] { 0x05, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0 });

                var reqBuf = new byte[1024];
                c.Receive(reqBuf);
                var response = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello");
                c.Send(response);
                c.Close();
                return;
            }
        });
        socksThread.Start();

        try
        {
            var client = new TorClient(new Dictionary<string, string>
            {
                ["ra.tor.socksPort"] = socksPort.ToString(),
                ["ra.tor.controlPort"] = controlPort.ToString(),
            });
            Assert.True(client.Start());
            Assert.Equal(Status.Connected, client.GetStatus());

            var env = Envelope.HeadersOnly();
            env.SetHeader("url", "http://example.onion/path");
            Assert.True(client.Send(env));

            var body = env.Header("body");
            Assert.NotNull(body);
            var bytes = body!.AsValue().GetValue<byte[]>();
            Assert.Equal("hello", Encoding.ASCII.GetString(bytes));
        }
        finally
        {
            socksThread.Join();
            socksListener.Stop();
            controlThread.Join();
            controlListener.Stop();
        }
    }
}
