<!-- markdownlint-disable MD024 -->

# Changelog

All notable changes to OrionLens are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-06-15

### Added

Initial release. Ambient correlation context.

- `CorrelationContext`: immutable correlation id plus baggage; `WithBaggage` returns a new
  instance.
- `OrionContext`: `AsyncLocal`-backed ambient context with nesting `BeginScope` and restore.
- `CorrelationPropagator`: HTTP-agnostic extract (mint id when missing) and inject, with
  percent-encoded baggage.
- `CorrelationMiddleware` + `UseOrionLens()`: establishes the context per request and echoes the
  id on the response.
- `CorrelationPropagationHandler`: an `HttpClient` `DelegatingHandler` that injects the ambient
  context into outbound requests.
- `CorrelationOptions`: header names, generate-when-missing, response-header echo; validated on
  registration.
- `AddOrionLens()` DI extension.

### Tests

21 tests across the context, the ambient store (nesting, async flow), the propagator (extract,
generate, round-trip, encoding), the middleware, the outbound handler, and registration.

[0.1.0]: https://github.com/tunahanaliozturk/OrionLens/releases/tag/v0.1.0
