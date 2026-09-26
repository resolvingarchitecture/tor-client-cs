using System.Net.Sockets;
using System.Text;
using Ra.Common;

namespace Ra.Tor;

/// <summary>
/// Minimal SOCKS5 CONNECT client (no auth) - enough to tunnel an HTTP
/// request through Tor's SOCKS proxy. Ports <c>tor-client-rust</c>'s
/// <c>socks</c> module.
///
/// Like the C++ port (and unlike TypeScript's <c>node:net</c>), .NET's
/// <see cref="Socket.Receive(byte[],int,int,SocketFlags)"/> blocks until
/// data arrives or <see cref="Socket.ReceiveTimeout"/> fires, so
/// <see cref="RecvExact"/> is a plain loop - no listener-lifecycle gotcha to
/// work around.
/// </summary>
internal static class Socks5
{
    /// <summary>
    /// Opens a TCP connection to <paramref name="destHost"/>:<paramref name="destPort"/>
    /// *through* the SOCKS5 proxy at <paramref name="proxyHost"/>:<paramref name="proxyPort"/>.
    /// Caller owns the returned socket.
    /// </summary>
    public static Socket ConnectThrough(string proxyHost, int proxyPort, string destHost, int destPort,
        TimeSpan timeout)
    {
        var socket = ConnectWithTimeout(proxyHost, proxyPort, timeout)
            ?? throw new RaException(RaErrorKind.Io, $"could not connect to SOCKS proxy {proxyHost}");

        var readTimeout = timeout > TimeSpan.FromSeconds(30) ? timeout : TimeSpan.FromSeconds(30);
        socket.ReceiveTimeout = (int)readTimeout.TotalMilliseconds;
        socket.SendTimeout = (int)readTimeout.TotalMilliseconds;

        try
        {
            // greeting: VER=5, NMETHODS=1, METHOD=0 (no auth)
            socket.Send([0x05, 0x01, 0x00]);
            var method = RecvExact(socket, 2);
            if (method[0] != 0x05 || method[1] != 0x00)
            {
                throw new RaException(RaErrorKind.Io, "SOCKS5 proxy refused no-auth");
            }

            // request: VER=5, CMD=1 (connect), RSV=0, ATYP=3 (domain), len, name, port
            var hostBytes = Encoding.ASCII.GetBytes(destHost);
            if (hostBytes.Length > 255)
            {
                throw RaException.Invalid("host too long");
            }
            var req = new List<byte> { 0x05, 0x01, 0x00, 0x03, (byte)hostBytes.Length };
            req.AddRange(hostBytes);
            req.Add((byte)(destPort >> 8));
            req.Add((byte)(destPort & 0xff));
            socket.Send(req.ToArray());

            // reply: VER, REP, RSV, ATYP, BND.ADDR, BND.PORT
            var head = RecvExact(socket, 4);
            if (head[1] != 0x00)
            {
                throw new RaException(RaErrorKind.Io, $"SOCKS5 connect failed, REP={head[1]}");
            }
            var bndLen = head[3] switch
            {
                0x01 => 4,
                0x04 => 16,
                0x03 => RecvExact(socket, 1)[0],
                _ => throw new RaException(RaErrorKind.Io, $"SOCKS5 bad ATYP {head[3]}"),
            };
            RecvExact(socket, bndLen + 2);
            return socket;
        }
        catch
        {
            socket.Close();
            throw;
        }
    }

    private static Socket? ConnectWithTimeout(string host, int port, TimeSpan timeout)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        try
        {
            var result = socket.BeginConnect(host, port, null, null);
            if (!result.AsyncWaitHandle.WaitOne(timeout))
            {
                socket.Close();
                return null;
            }
            socket.EndConnect(result);
            return socket;
        }
        catch (SocketException)
        {
            socket.Close();
            return null;
        }
    }

    /// <summary>Reads exactly <paramref name="n"/> bytes from an already-connected socket.</summary>
    internal static byte[] RecvExact(Socket socket, int n)
    {
        var buf = new byte[n];
        var got = 0;
        while (got < n)
        {
            int r;
            try
            {
                r = socket.Receive(buf, got, n - got, SocketFlags.None);
            }
            catch (SocketException e) when (e.SocketErrorCode is SocketError.TimedOut)
            {
                throw new RaException(RaErrorKind.Io, "read timeout");
            }
            if (r == 0)
            {
                throw new RaException(RaErrorKind.Io, "connection closed early");
            }
            got += r;
        }
        return buf;
    }
}
