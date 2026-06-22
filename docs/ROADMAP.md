# Roadmap

OrionLens is an ambient correlation-context library for .NET: a correlation id plus a little baggage,
established at the edge of a request and carried through every `await` and across downstream calls,
with HTTP-agnostic propagation, an ASP.NET Core middleware, and an outbound `HttpClient` handler.

The current release is **0.3.0**. The sections below record what has shipped and the directions under
consideration next. Forward items are directions, not commitments, and the version groupings indicate
likely sequencing rather than firm dates. If an item matters to your workload, open an issue and say
so; real demand is what moves things up the list.

## Released

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

- **Deeper Activity integration.** Build on the existing trace-context bridge and
  `OrionTraceContextScope`: emit baggage as activity tags or baggage on the started activity, and add
  a single registration helper so the OrionLens `ActivitySource` is wired into OpenTelemetry without
  a manual `AddSource`. The bridge itself already ships; this is the integration depth on top of it.
- **Sampling-aware correlation.** Today `traceparent` is emitted with the recorded flag taken from a
  live `Activity`, but the inbound sampling decision is not read back into the correlation surface.
  Carry the sampled flag through `Extract`, and let baggage policy react to it (for example, attach
  heavier diagnostic baggage only on sampled requests).
- **W3C `baggage` interop.** An optional propagator that reads and writes the standard W3C `baggage`
  header alongside (or instead of) the custom `X-Orion-Baggage` format, so OrionLens baggage
  interoperates with other systems that already speak W3C baggage. Selectable through configuration so
  the existing format stays the default.
- **Transport adapters.** Thin, separately shipped adapters that carry the correlation context across
  common message brokers and gRPC, building on the already transport-agnostic `CorrelationPropagator`.
  The core would stay HTTP-free; each adapter would be opt-in.

## Out of scope (for now)

- Becoming a full tracing system. OrionLens is a correlation-context primitive; distributed-tracing
  backends remain a separate concern that the trace-context bridge feeds, not replaces.
- Bundling a specific logging or telemetry vendor. Enrichment and propagator work stays opt-in and
  dependency-light.

Feedback and pull requests are welcome. See [CONTRIBUTING.md](../CONTRIBUTING.md).
