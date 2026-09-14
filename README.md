# tor-client (C#)

A local-only Tor client for **1M5**: attaches to a Tor daemon already
running on this host — SOCKS5 proxy `127.0.0.1:9050`, control port
`127.0.0.1:9051` (probed for readiness only).

A C# port of [`tor-client-java`](https://github.com/resolvingarchitecture/tor-client-java);
mirrors the *local* backend of [`tor-client-rust`](https://github.com/resolvingarchitecture/tor-client-rust)
(no embedded backend — Arti is Rust-only, see `DESIGN.md`).

## Local Tor daemon setup

Install Tor (`apt install tor`, `brew install tor`, …) and make sure
`/etc/tor/torrc` (or `~/.torrc`) has:

```
SocksPort 9050
ControlPort 9051
CookieAuthentication 0
```

Then `systemctl start tor` (or `tor -f ~/.torrc`). Check:
`curl --socks5-hostname 127.0.0.1:9050 https://check.torproject.org/api/ip`.

## Use

```csharp
using Ra.Common;
using Ra.TorClient;

var client = new TorClient();                 // or new TorClient(config)
if (client.Start())                            // false (cleanly) if Tor is unavailable
{
    var env = Envelope.HeadersOnly();
    env.SetHeader("url", "http://example.onion/");
    client.Send(env);                          // body -> env.Headers["body"], errors -> env.Headers["error"]
}
```

### Config keys

| key | default | meaning |
|-----|---------|---------|
| `ra.tor.host` | `127.0.0.1` | local daemon host |
| `ra.tor.socksPort` | `9050` | local daemon SOCKS5 proxy port |
| `ra.tor.controlPort` | `9051` | local daemon control port (probed only) |
| `ra.tor.requestTimeoutSecs` | `60` | per-request timeout |

## Build

```
dotnet build
dotnet test
```

Depends on `ra-common-cs` via a relative `ProjectReference`
(`../../common/ra-common-cs`), matching `seda-bus-cs`/`service-bus-cs`'s
monorepo-dependency convention.

## Status

Early. HTTP (`http://`) works; HTTPS needs `SslStream` wrapped around the
SOCKS socket (see `TODO.md`). The local daemon's control port is only
probed, not spoken — no event stream or hidden-service management yet.
Inbound / onion hosting is not implemented. See `DESIGN.md` and `TODO.md`.
