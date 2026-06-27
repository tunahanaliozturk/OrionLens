<!-- markdownlint-disable MD024 -->

# Changelog

All notable changes to OrionLens are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.4.0] - 2026-06-27

### Added

- Deeper `System.Diagnostics.Activity` integration, opt-in through `CorrelationOptions.AlignWithActivity`. On extract, when no inbound correlation id header is present and a W3C `Activity` is current, that activity's trace-id seeds the correlation id (ahead of any inbound `traceparent`). When the ASP.NET Core middleware makes a context current, the correlation id is written onto the current `Activity` as a tag (`ActivityCorrelationTag`, default `orion.correlation_id`) and the keys in `ActivityBaggageKeys` are copied onto the activity's baggage, so OrionLens and `Activity`-based tracing agree on the identifier. This never starts a span; an absent activity is left untouched. `OrionTraceContextScope.AlignCurrentActivity` exposes the projection directly for non-middleware hosts.
- Sampling-aware correlation. `CorrelationContext.IsSampled` carries the head-based sampling decision, seeded on extract from the current `Activity`'s recorded flag or the inbound `traceparent` sampled bit (and otherwise true, the behaviour-compatible default). A derived outbound `traceparent` now reflects the decision in its flags field instead of always emitting `00`. `CorrelationOptions.SampledOnlyBaggageKeys` marks baggage that rides only a sampled trace, so heavier diagnostic baggage travels only with traces a backend keeps. The correlation id always propagates for logging regardless of the decision, and no span is ever force-created.
- W3C `baggage` header interop, opt-in through `CorrelationOptions.UseW3CBaggage`. On extract both the custom `X-Orion-Baggage` header and the standard `baggage` header (`W3CBaggageHeader`) are parsed, with the custom header authoritative on a key collision. On inject the same policy-filtered payload is written to both headers, so the 0.3.0 baggage policy still holds: non-propagating keys stay off the wire on both channels. With interop off, the custom format remains the sole baggage channel and the wire output is unchanged.

### Notes

- The roadmap's **transport adapters** item (message-broker and gRPC carriers) remains deferred: it ships as separate opt-in adapter packages, outside this core-package release.

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
