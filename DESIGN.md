# tor-client (C#) — Design

A local-only Tor client: attaches to a Tor daemon already running on the
host via its SOCKS proxy, for use as the Tor **protocol service** by a
future `1m5-core-cs`. A C# port of the design in
[`tor-client-java`](https://github.com/resolvingarchitecture/tor-client-java),
trimmed to the same scope `tor-client-rust`'s *local* backend covers — same
scope cut `tor-client-python`/`tor-client-ts`/`tor-client-cpp` already made.

## Where it sits

    (future) 1m5-core-cs  ──wraps──►  Ra.TorClient.TorClient
                                              │
                                 SOCKS5 127.0.0.1:9050
                                 control 127.0.0.1:9051 (probe only)
                                              │
                                      system tor daemon

## No embedded backend

Same reasoning as every other non-Rust port: Rust's `embedded` backend runs
[Arti](https://gitlab.torproject.org/tpo/core/arti), the Tor Project's
pure-**Rust** Tor implementation, in-process — there is no C# equivalent to
embed. So, like `tor-client-java`, this client only ever attaches to a Tor
instance **installed and running on the host**. No `Mode`/`Backend` split,
no `ra.tor.mode`/`ra.tor.dataDir` config keys.

## Components

    LocalTorDetector  probes SOCKS 9050 + control 9051 (TcpClient.BeginConnect
                       + AsyncWaitHandle.WaitOne(timeout) - the standard .NET
                       sync-connect-with-timeout pattern; a bare Connect() has
                       no timeout parameter)
    Socks5            minimal SOCKS5 CONNECT client, no auth, on raw Socket
    Http              FetchViaSocks / ParseUrl / FormatGet / SplitBody;
                       http:// only, no TLS
    TorClient         config, status, Start()/Stop()/Send()

`Ra.TorClient` is both the namespace and the client's class name
(`Ra.TorClient.TorClient`) — same pattern `service-bus-cs` already uses
(`Ra.ServiceBus.ServiceBus`), not a naming mistake.

## Message flow

**Outbound** — a caller sets `envelope.SetHeader("url", ...)` to a `.onion`
or clearnet `http://` URL and calls `Send()`. `Http.FetchViaSocks` opens a
SOCKS5 tunnel through `127.0.0.1:9050`, issues a `GET`, and writes the
response body to `envelope.Headers["body"]` as a `JsonValue` wrapping a
`byte[]`.

`Ra.Common.Envelope.Headers` is `Dictionary<string, JsonNode?>` with no
generic byte-payload field the way `seda_bus::Envelope` does in Rust — same
gap every other port hit, same `headers["body"]` fix. `System.Text.Json`
has no distinct binary `JsonNode` kind (unlike nlohmann::json's binary
subtype in the C++ port) — `JsonValue.Create(byte[])` serializes to a
base64 string, which is `System.Text.Json`'s own convention for binary
data, so this needed no extra encoding decision.

**Inbound** — not implemented (see `TODO.md`), same as every other port.

## Status model

`Status` is its own 4-state enum (`Connecting`, `Connected`, `Disconnected`,
`Error`), not `Ra.Common`'s `ServiceStatus` — matches every other port.
Backed by a plain `volatile Status` field rather than a lock or an atomic
wrapper type: .NET's `volatile` keyword is valid directly on an
enum-typed field (the CLR guarantees atomic reads/writes of values that fit
a machine word, and `volatile` adds the memory-visibility guarantee), so
there's no C++-style movability wrinkle to design around - simplest of any
port so far. `Start()` sets `Connecting`, then `Connected` if the daemon
answers or `Disconnected` (returns `false`, never throws) if not. `Stop()`
→ `Disconnected`.

## Config keys

Same names as the other ports (`ra.tor.mode`/`ra.tor.dataDir` dropped — no
embedded mode to select): `ra.tor.host`, `ra.tor.socksPort`,
`ra.tor.controlPort`, `ra.tor.requestTimeoutSecs`.

## C# adaptations vs. the other ports

- **Connect-with-timeout via `BeginConnect`/`EndConnect`**, not
  `Socket.Connect` (which has no timeout overload) and not
  `ConnectAsync(...).Wait(timeout)` (which leaves the connect attempt
  running in the background after a timeout instead of aborting it) — the
  classic .NET pattern: start the async connect, wait on its
  `AsyncWaitHandle` with a timeout, and `Close()` the socket if it doesn't
  signal in time (closing aborts the pending connect cleanly).
- **Exceptions use `Ra.Common.RaException`/`RaErrorKind`**, matching
  `ra-common-cs`'s own package-wide error type rather than inventing one
  (same decision the C++ port made with `ra::common::RaException`).
- **Blocking `Socket`, no listener-lifecycle gotcha.** `Socks5.RecvExact`
  loops a blocking `Socket.Receive` until it has `n` bytes or
  `Socket.ReceiveTimeout` throws — straightforward, like the C++ port and
  unlike `tor-client-ts`'s `SocketReader`, which exists specifically to
  work around `node:net`'s non-blocking, event-driven stream API.
- SOCKS5 + HTTP hand-rolled on `System.Net.Sockets.Socket` (no
  `HttpClient`/`SocksHttpHandler`), matching every other port's
  dependency-light default.
- The Tor control protocol client (`TORControlConnection` & friends) is not
  ported, same gap every other port has.

## Not here

- HTTPS (needs `System.Net.Security.SslStream` wrapped around the SOCKS
  socket).
- Tor control protocol: authentication, event stream, `NEWNYM`, circuit info.
- Hidden service (onion) hosting for inbound envelopes.
- Stream isolation per identity / per destination.
- An embedded/bridged backend (see "No embedded backend").
