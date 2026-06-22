<!-- markdownlint-disable MD024 -->

# Changelog

All notable changes to OrionLens are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0] - 2026-06-22

### Added

- Logging enrichment helpers. `ILogger.BeginCorrelationScope(...)` opens a logging scope carrying the correlation id (and the baggage keys selected by `CorrelationOptions.LoggedBaggageKeys`) so structured logs carry it without a manual `BeginScope` at every log site. The scope state, `CorrelationLogScope`, is an `IReadOnlyList<KeyValuePair<string, object>>` that structured providers lift into named properties and that renders as text via `ToString` for providers that format a scope as a single string. The helpers build only on `Microsoft.Extensions.Logging.Abstractions` and bind no logging sink. An ambient overload returns null when no context is established; an explicit-context overload enriches from a supplied `CorrelationContext` for background jobs and message consumers. Only opted-in baggage keys reach the scope, keeping internal or high-cardinality values out of logs.
- Baggage policy on `CorrelationOptions`, enforced at propagation time. `MaxBaggageCount` caps the number of baggage pairs and `MaxBaggageBytes` caps the encoded header size; both are enforced on inbound extract and outbound inject by dropping the pairs that would exceed the limit (in ordinal key order, so the kept set is deterministic) rather than throwing, so a policy breach never fails a live request. `NonPropagatingBaggageKeys` marks keys inbound-only: such a key is accepted on extract but never written on inject, so an internal value is not leaked across a trust boundary. Caps are validated as positive on registration. With no policy configured, outbound formatting takes a fast path that keeps the prior wire output and cost exactly.

## [0.2.1] - 2026-06-20

### Performance

- Trace-context bridge on the inject hot path now allocates less and does less work. `W3CTraceContext.ToTraceId` encodes the correlation id into a stack buffer and writes the trace-id as lowercase hex directly into a span, removing the per-call `byte[]` and the uppercase-then-lowercase double string allocation (about 248 to 88 bytes per hashed id, the residual 88 being the returned string). `CorrelationPropagator.Inject` derives the trace-id once and reuses it instead of hashing the correlation id a second time inside `Format` when bridging to the W3C trace context. No public API or wire-format change; all existing tests pass unchanged.

## [0.2.0] - 2026-06-19

### Added
- W3C trace-context bridge: link the correlation id to the current Activity / W3C `traceparent` so the correlation id and the distributed trace id align across systems.

### Fixed
- `CorrelationPropagator.Extract` with `GenerateIdWhenMissing = false` no longer substitutes the literal `"unknown"` for a missing inbound id; it now returns the value verbatim (empty when absent) as documented. This is a behavior change for callers that relied on the `"unknown"` sentinel.

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
