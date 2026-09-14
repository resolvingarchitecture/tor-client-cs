using System.Text.Json.Nodes;
using Ra.Common;

namespace Ra.TorClient;

/// <summary>
/// <c>TorClient</c> - the local-only Tor client. Ports <c>tor-client-rust</c>'s
/// <c>TorClient</c> minus the <c>Mode</c>/embedded-backend split: Arti (the
/// pure-Rust Tor implementation <c>tor-client-rust</c> embeds) has no C#
/// equivalent, so this port - like <c>tor-client-java</c> - only ever
/// attaches to a Tor daemon already running on the host. See <c>DESIGN.md</c>.
/// </summary>
public enum Status
{
    Connecting,
    Connected,
    Disconnected,
    Error,
}

/// <summary>
/// A Tor client attached to a local Tor daemon's SOCKS proxy (default
/// <c>127.0.0.1:9050</c>); the control port (default <c>9051</c>) is probed
/// for readiness only - see <c>TODO.md</c> for the control protocol.
/// </summary>
public sealed class TorClient
{
    public LocalTorDetector Detector { get; } = new();
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);

    private volatile Status _status = Status.Disconnected;

    public TorClient() { }

    /// <summary>Config keys: <c>ra.tor.host</c>, <c>ra.tor.socksPort</c>,
    /// <c>ra.tor.controlPort</c>, <c>ra.tor.requestTimeoutSecs</c>.</summary>
    public TorClient(IReadOnlyDictionary<string, string> config)
    {
        if (config.TryGetValue("ra.tor.host", out var host)) Detector.Host = host;
        if (config.TryGetValue("ra.tor.socksPort", out var socksPort)) Detector.SocksPort = int.Parse(socksPort);
        if (config.TryGetValue("ra.tor.controlPort", out var controlPort)) Detector.ControlPort = int.Parse(controlPort);
        if (config.TryGetValue("ra.tor.requestTimeoutSecs", out var timeoutSecs))
        {
            RequestTimeout = TimeSpan.FromSeconds(double.Parse(timeoutSecs));
        }
    }

    public static TorClient FromConfig(IReadOnlyDictionary<string, string> config) => new(config);

    public Status GetStatus() => _status;

    /// <summary>Probes the local daemon. Returns <c>false</c> cleanly (never
    /// throws) if Tor is unavailable.</summary>
    public bool Start()
    {
        _status = Status.Connecting;
        if (!Detector.IsLocalTorRunning())
        {
            Console.Error.WriteLine(
                $"No local Tor daemon on {Detector.Host} (SOCKS {Detector.SocksPort} " +
                $"reachable={Detector.IsSocksReachable()}, control {Detector.ControlPort} " +
                $"reachable={Detector.IsControlReachable()}). Install and run Tor with " +
                "'ControlPort 9051' - see README.md.");
            _status = Status.Disconnected;
            return false;
        }
        _status = Status.Connected;
        return true;
    }

    public bool Stop()
    {
        _status = Status.Disconnected;
        return true;
    }

    /// <summary>
    /// Fetches <c>envelope.Header("url")</c> through Tor into
    /// <c>envelope.Headers["body"]</c>. HTTP only for now. On error, records
    /// <c>envelope.Headers["error"]</c>.
    ///
    /// Uses headers rather than a generic payload field - <see cref="Envelope"/>
    /// carries a typed <c>Message</c>, not a raw byte payload, same gap every
    /// other port hit; same fix applied here, storing the body as a
    /// <see cref="JsonValue"/> wrapping a <c>byte[]</c> (serializes to base64,
    /// System.Text.Json's own convention for binary data - there is no
    /// separate binary <see cref="JsonNode"/> kind the way nlohmann::json has).
    /// </summary>
    public bool Send(Envelope envelope)
    {
        var url = envelope.Header("url")?.GetValue<string>();
        if (string.IsNullOrEmpty(url))
        {
            envelope.SetHeader("error", "no url header");
            return false;
        }

        if (GetStatus() != Status.Connected)
        {
            envelope.SetHeader("error", "Tor client not started");
            return false;
        }

        try
        {
            var body = Http.FetchViaSocks(Detector.Host, Detector.SocksPort, url, RequestTimeout);
            envelope.SetHeader("body", JsonValue.Create(body));
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Tor request to {url} failed: {e.Message}");
            envelope.SetHeader("error", e.Message);
            return false;
        }
    }
}
