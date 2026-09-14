# tor-client (C#) — TODO

## P0 — local client (done)

- [x] `LocalTorDetector` (probe SOCKS 9050 + control 9051 via
      `BeginConnect`/`AsyncWaitHandle.WaitOne`); `Start()` fails fast with an
      actionable stderr message when no daemon is reachable.
- [x] Hand-rolled SOCKS5 CONNECT (no auth) on `Socket`.
- [x] HTTP/1.1 GET through the SOCKS tunnel; response body on
      `envelope.Headers["body"]` as a `JsonValue`-wrapped `byte[]`.
- [x] Config keys aligned with the other ports: `ra.tor.host`,
      `ra.tor.socksPort`, `ra.tor.controlPort`, `ra.tor.requestTimeoutSecs`.

## P1 — request path

- [ ] HTTPS (`System.Net.Security.SslStream` wrapped around the SOCKS socket).
- [ ] Follow redirects; surface status code + headers.
- [ ] Reuse the SOCKS connection / a small pool instead of one per request.
- [ ] Configurable `User-Agent`; strip identifying headers by default.
- [ ] Async variant (`Task`-based `StartAsync`/`SendAsync`) once a real
      caller needs it — the sync `Socket` client is deliberately the v1
      scope, matching the C++ port's choice.

## P2 — Tor control protocol

- [ ] Port `TORControlConnection` / `TORControlCommands` from
      `tor-client-java` (authenticate with `CookieAuthentication 0` or a
      control password).
- [ ] Async event stream (`SETEVENTS`) → map `CIRC` / `STATUS_CLIENT` onto
      `Status`; live readiness instead of a one-shot probe.
- [ ] `NEWNYM` (new circuit) on demand.

## P3 — inbound / hidden service

- [ ] Create or load an onion service key, `ADD_ONION` via the control port.
- [ ] Accept connections on the HS target port, turn requests into
      `Envelope`s (mirrors `tor-client-java`'s HS handler).

## P4 — privacy hardening

- [ ] Stream isolation: distinct SOCKS credentials per identity / destination.
- [ ] Optional bridge / pluggable-transport config passthrough (needs the
      system Tor's own bridge config — no embedded backend here to configure).

## Testing / ops

- [ ] Integration test behind a trait/category that uses a real local Tor
      daemon.
- [x] Fake-SOCKS-proxy integration test (`ClientTests.cs`), no live network.
- [ ] CI: `dotnet build` + `dotnet test`.
- [ ] Publish to NuGet once the API settles (currently `ProjectReference`
      only).

## Cross-repo

- [ ] Keep `Status` and config keys aligned with `tor-client-java` 1.2.x and
      `tor-client-rust`'s local backend.
- [ ] Wire into a future `1m5-core-cs`'s protocol-service adapter, same
      pattern as `NetworkServiceProtocol`/`TorProtocolService` in
      `1m5-core-java` and `1m5-core-rust`.
