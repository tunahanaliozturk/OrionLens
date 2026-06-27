# Roadmap

OrionLens is an ambient correlation-context library for .NET: a correlation id plus a little baggage,
established at the edge of a request and carried through every `await` and across downstream calls,
with HTTP-agnostic propagation, an ASP.NET Core middleware, and an outbound `HttpClient` handler.

The current release is **0.4.0**. The sections below record what has shipped and the directions under
consideration next. Forward items are directions, not commitments, and the version groupings indicate
likely sequencing rather than firm dates. If an item matters to your workload, open an issue and say
so; real demand is what moves things up the list.

## Released

### 0.4.0 (2026-06-27)

- Deeper Activity integration. With `AlignWithActivity` enabled, extract seeds the correlation id from
  the current W3C `Activity`'s trace-id when no id header is present, and the middleware projects the
  correlation id (as the `ActivityCorrelationTag` tag) and the opted-in `ActivityBaggageKeys` onto the
  current `Activity`, so OrionLens and `Activity`-based tracing agree on the identifier.
  `OrionTraceContextScope.AlignCurrentActivity` exposes the projection for non-middleware hosts. This
  never starts a span; an absent activity is left untouched.
- Sampling-aware correlation. `CorrelationContext.IsSampled` carries the head-based sampling decision,
  seeded from the current `Activity`'s recorded flag or the inbound `traceparent` sampled bit (and
  otherwise true). A derived outbound `traceparent` reflects the decision in its flags field, and
  `SampledOnlyBaggageKeys` lets heavier diagnostic baggage ride only sampled traces. The correlation id
  always propagates for logging regardless of the decision, and the bridge never force-creates a span.
- W3C `baggage` interop. With `UseW3CBaggage` enabled, the standard `baggage` header is read and written
  alongside the custom `X-Orion-Baggage` format (the custom header wins on a key collision), with the
  0.3.0 baggage policy applied to both so non-propagating keys stay off the wire on either channel.
  Off by default, so the custom format stays the default.

### 0.3.0 (2026-06-22)

- Logging enrichment helpers. `ILogger.BeginCorrelationScope(...)` pushes the correlation id (and the
  baggage keys selected by `CorrelationOptions.LoggedBaggageKeys`) into a logging scope, so structured
  logs carry it without a manual `BeginScope` at every log site. The scope state is a structured
  name/value list that providers lift into named properties and that also renders as text. The helpers
  build only on `Microsoft.Extensions.Logging.Abstractions` and bind no logging sink; an explicit
  context overload serves background jobs and message consumers that hold a context directly. Only
  opted-in baggage keys reach the scope.
- Baggage policy on `CorrelationOptions`, enforced at inject and extract. `MaxBaggageCount` and
  `MaxBaggageBytes` bound the pair count and the encoded header size by dropping the pairs that would
  exceed the cap (in ordinal key order) rather than throwing, so a policy breach never fails a live
  request. `NonPropagatingBaggageKeys` marks keys inbound-only: accepted on extract, never written on
  inject, so an internal value is not leaked across a trust boundary. With no policy configured,
  formatting keeps its prior wire output and cost.

### 0.2.1 (2026-06-20)

- Allocation-free trace-id derivation on the inject hot path. `W3CTraceContext.ToTraceId` encodes the
  correlation id into a stack buffer and writes the trace-id as lowercase hex directly into a span,
  removing the per-call `byte[]` and the double string allocation; `CorrelationPropagator.Inject`
  derives the trace-id once and reuses it instead of hashing the id a second time. No public API or
  wire-format change.

### 0.2.0 (2026-06-19)

- W3C trace-context bridge. With `UseTraceContext` enabled, the correlation id is aligned with the
  W3C `traceparent`: on extract, an inbound `traceparent` trace-id becomes the correlation id when no
  id header is present; on inject, a `traceparent` is emitted whose trace-id is derived from the
  correlation id, so it never conflicts with `X-Correlation-ID`. The `TraceParentHeader` name is
  configurable.
- `OrionTraceContextScope.BeginTraceLinkedScope` reconciles the ambient correlation id with the
  current `System.Diagnostics.Activity` (adopting a live W3C trace-id, or starting an activity whose
  trace-id derives from the correlation id) from a public `ActivitySource("Moongazing.OrionLens")`
  that an OpenTelemetry `AddSource` call can record.
- `Extract` with `GenerateIdWhenMissing = false` returns a missing inbound id verbatim (empty by
  default) rather than substituting a `"unknown"` sentinel.

### 0.1.0 (2026-06-15)

- Initial release: the immutable `CorrelationContext`, the `AsyncLocal`-backed `OrionContext` ambient
  store with nesting scopes, the HTTP-agnostic `CorrelationPropagator`, the `CorrelationMiddleware`
  (`UseOrionLens()`), the outbound `CorrelationPropagationHandler`, `CorrelationOptions`, and the
  `AddOrionLens()` DI extension.

## Under consideration

### Near term

- **Transport adapters.** Thin, separately shipped adapters that carry the correlation context across
  common message brokers and gRPC, building on the already transport-agnostic `CorrelationPropagator`.
  The core would stay HTTP-free; each adapter would be opt-in. Still planned: this ships as separate
  adapter packages, deliberately outside the core `Moongazing.OrionLens` package, so it is deferred
  past 0.4.0 rather than bundled into the core.
- **OpenTelemetry registration helper.** A single registration call so the OrionLens `ActivitySource`
  is wired into an OpenTelemetry `TracerProvider` without a manual `AddSource`. The 0.4.0 Activity
  integration projects the correlation id and baggage onto a live `Activity`; this would remove the
  remaining manual `AddSource` step for OpenTelemetry hosts.

## Out of scope (for now)

- Becoming a full tracing system. OrionLens is a correlation-context primitive; distributed-tracing
  backends remain a separate concern that the trace-context bridge feeds, not replaces.
- Bundling a specific logging or telemetry vendor. Enrichment and propagator work stays opt-in and
  dependency-light.

Feedback and pull requests are welcome. See [CONTRIBUTING.md](../CONTRIBUTING.md).
