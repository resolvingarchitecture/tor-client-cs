# Changelog

## 0.1.0

- Initial local-only Tor client: `LocalTorDetector`, hand-rolled SOCKS5 +
  HTTP/1.1 GET on `System.Net.Sockets.Socket`, `TorClient` (`FromConfig`/
  `Start`/`Stop`/`Send`).
- Depends on `Ra.Common` for `Envelope`; uses `Ra.Common.RaException` for
  errors.
- No embedded backend (see `DESIGN.md`).
