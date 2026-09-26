using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Ra.Tor.Tests;

public class DetectorTests
{
    private static (TcpListener listener, int port) Listen()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return (listener, ((IPEndPoint)listener.LocalEndpoint).Port);
    }

    [Fact]
    public void UnreachablePortIsNotReachable()
    {
        var d = new LocalTorDetector { SocksPort = 1, ControlPort = 1, Timeout = TimeSpan.FromMilliseconds(200) };
        Assert.False(d.IsSocksReachable());
        Assert.False(d.IsLocalTorRunning());
    }

    [Fact]
    public void ReachablePortIsReachable()
    {
        var (listener, port) = Listen();
        try
        {
            var d = new LocalTorDetector { SocksPort = port, Timeout = TimeSpan.FromMilliseconds(500) };
            Assert.True(d.IsSocksReachable());
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void IsLocalTorRunningRequiresBothPorts()
    {
        var (listener, port) = Listen();
        try
        {
            var d = new LocalTorDetector { SocksPort = port, ControlPort = 1, Timeout = TimeSpan.FromMilliseconds(200) };
            Assert.True(d.IsSocksReachable());
            Assert.False(d.IsControlReachable());
            Assert.False(d.IsLocalTorRunning());
        }
        finally
        {
            listener.Stop();
        }
    }
}
