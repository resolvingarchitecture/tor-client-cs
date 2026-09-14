using System.Net.Sockets;

namespace Ra.TorClient;

/// <summary>
/// Detects a local Tor daemon.
///
/// Ports <c>tor-client-rust</c>'s <c>detector</c> module (itself a port of
/// <c>tor-client-java</c>'s <c>LocalTorDetector</c>). Tor is a C daemon -
/// unlike I2P, which has a pure-language router this project can embed - so
/// every language port only ever attaches to a Tor instance already
/// installed and running on the host.
/// </summary>
public sealed class LocalTorDetector
{
    public string Host { get; set; } = "127.0.0.1";
    public int SocksPort { get; set; } = 9050;
    public int ControlPort { get; set; } = 9051;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(750);

    public bool IsSocksReachable() => Reachable(SocksPort);
    public bool IsControlReachable() => Reachable(ControlPort);

    /// <summary>True only if both the SOCKS proxy and the control port answer.</summary>
    public bool IsLocalTorRunning() => IsSocksReachable() && IsControlReachable();

    private bool Reachable(int port)
    {
        using var client = new TcpClient();
        try
        {
            var result = client.BeginConnect(Host, port, null, null);
            if (!result.AsyncWaitHandle.WaitOne(Timeout))
            {
                client.Close();
                return false;
            }
            client.EndConnect(result);
            return client.Connected;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
