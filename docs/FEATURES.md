# Features

A deeper look at every public type in OrionLens and how it behaves. Everything here is backed by the
source under `src/Moongazing.OrionLens` and exercised by the test suite under
`tests/Moongazing.OrionLens.Tests`.

The package id is `OrionLens`. The root namespace is `Moongazing.OrionLens`, with the context types
under `Moongazing.OrionLens.Context`, the middleware under `Moongazing.OrionLens.AspNetCore`, the
outbound handler under `Moongazing.OrionLens.Http`, and the logging helpers under
`Moongazing.OrionLens.Logging`.

---

## The data model: `CorrelationContext`

`Moongazing.OrionLens.Context.CorrelationContext` is an immutable snapshot of the ambient request
context: a correlation id plus baggage.

- **`CorrelationId`** - the id for the current logical operation.
- **`Baggage`** - the baggage as an `IReadOnlyDictionary<string, string>`, backed internally by a
  `FrozenDictionary` for fast lookups.
- **`Create(string correlationId)`** - a context with an id and no baggage. Throws if the id is null
  or empty.
- **`Create(string correlationId, IReadOnlyDictionary<string, string> baggage)`** - a context with an
  id and an initial baggage set, copied into a frozen dictionary with ordinal key comparison.
- **`GetBaggage(string key)`** - the value for a key, or `null` if absent. Throws if the key is null
  or empty.
- **`WithBaggage(string key, string value)`** - returns a *new* context with one baggage pair added
  or replaced. The original is untouched, which is what makes nested scopes safe.
- **`IsSampled`** - the head-based sampling decision, read on extract from the current `Activity`'s
  recorded flag (with `AlignWithActivity`) or the inbound `traceparent` flags (with
  `UseTraceContext`), and `true` otherwise. It gates `SampledOnlyBaggageKeys` and sets the flags of a
  derived outbound `traceparent`; it never forces a span.
- **`WithSampled(bool isSampled)`** - returns a context with the same id and baggage and the given
  sampling decision (the same instance when the value does not change).

Immutability is the central design choice: because `WithBaggage` returns a new instance rather than
mutating in place, a child scope can add baggage without ever disturbing the context its parent will
be restored to.

---

## The ambient store: `OrionContext`

`Moongazing.OrionLens.Context.OrionContext` is a static ambient accessor backed by
`AsyncLocal<CorrelationContext?>`, so the context follows `await` boundaries without being threaded
through method signatures.

- **`Current`** - the current context, or `null` when none has been established on this flow.
- **`BeginScope(CorrelationContext context)`** - sets the ambient context for the current flow and
  returns an `IDisposable` that restores the previous context on dispose.

Scopes nest. Disposing restores exactly what was current before the scope opened, and the returned
scope guards against double-dispose with an interlocked flag, so disposing twice is a no-op rather
than a corruption of the ambient state. `AsyncLocal` semantics mean the context flows into tasks
started inside the scope but does not leak back out to the caller after the scope is disposed.

---

## HTTP-agnostic propagation: `CorrelationPropagator`

`Moongazing.OrionLens.Context.CorrelationPropagator` is a static helper that moves a context in and
out of headers without depending on any particular HTTP type. Header access is expressed as a getter
and a setter, so the same code serves an ASP.NET request, an `HttpClient` request, or a message
envelope.

- **`Extract(Func<string, string?> getHeader, CorrelationOptions options)`** - builds a context from
  inbound headers. It reads the id header; when that is absent the id is, in order: the current W3C
  `Activity`'s trace-id (when `AlignWithActivity` is set), the inbound `traceparent` trace-id (when
  `UseTraceContext` is set and the header is valid), a new id (a `Guid` in `N` format) when
  `GenerateIdWhenMissing` is set, and otherwise `MissingIdSentinel` taken verbatim (empty by default).
  It then records the sampling decision (see `IsSampled`) and adds each decoded pair of the baggage
  header, plus the W3C `baggage` header when `UseW3CBaggage` is set (the custom header wins on a key
  collision). `MaxBaggageCount` and `MaxBaggageBytes` stop parsing at the cap, so the first pairs in
  header order are kept; `NonPropagatingBaggageKeys` are still accepted inbound.
- **`Inject(CorrelationContext context, Action<string, string> setHeader, CorrelationOptions options)`** -
  writes a context's id into the id header, and, when there is baggage, writes the baggage header
  (and the W3C `baggage` header when `UseW3CBaggage` is set). With a baggage policy configured it drops
  `NonPropagatingBaggageKeys`, drops `SampledOnlyBaggageKeys` on an unsampled context, sorts the
  remaining keys ordinally and applies the count and byte caps; the header is omitted when nothing
  survives. With `UseTraceContext` set it also writes a `traceparent` whose trace-id is derived from
  the correlation id (a 32-lowercase-hex id is used as-is, any other id is hashed) and whose flags
  reflect `IsSampled`; a live W3C `Activity` is reused only when its trace-id already matches.

Baggage on the wire is a comma-joined list of `key=value` pairs, with each key and value
percent-encoded (`Uri.EscapeDataString`) on the way out and decoded on the way in. Malformed pairs
(no `=`, or an empty key) are skipped on parse rather than throwing. The policy never throws: a pair
over a cap is dropped, so a breach cannot fail a live request.

---

## Trace-linked scopes: `OrionTraceContextScope`

`Moongazing.OrionLens.Context.OrionTraceContextScope` links the ambient context to
`System.Diagnostics.Activity`.

- **`Source`** - the `ActivitySource` named `Moongazing.OrionLens`; add it to a listener or an
  OpenTelemetry `AddSource` call to record the activities it starts.
- **`BeginTraceLinkedScope(CorrelationContext context, string activityName = "orion.scope")`** - when
  a W3C `Activity` is current, begins a scope whose correlation id is that activity's trace-id
  (baggage kept); otherwise starts an activity from `Source` (when a listener is attached) whose
  trace-id is derived from the correlation id, and begins the scope with the context as given. Disposing restores the previous
  context and stops any activity it started. The middleware uses it when `UseTraceContext` is set.
- **`AlignCurrentActivity(CorrelationContext context, string correlationTag, ISet<string>? baggageKeys = null)`** -
  writes the correlation id onto `Activity.Current` as a tag and copies the named baggage keys onto
  the activity's baggage (a key the activity already carries is left as-is). Does nothing when no
  activity is current or the id is empty, and never starts a span. The middleware calls it when
  `AlignWithActivity` is set.

---

## Logging enrichment: `OrionLensLoggerExtensions` and `CorrelationLogScope`

`Moongazing.OrionLens.Logging.OrionLensLoggerExtensions` opens an `ILogger` scope carrying the
correlation id, so every log inside a `using` carries it.

- **`BeginCorrelationScope(this ILogger logger)`** - scope with the ambient correlation id, or `null`
  when there is no ambient context.
- **`BeginCorrelationScope(this ILogger logger, CorrelationOptions options)`** - also includes the
  baggage keys in `LoggedBaggageKeys`.
- **`BeginCorrelationScope(this ILogger logger, CorrelationContext context, IEnumerable<string>? loggedBaggageKeys = null)`** -
  enriches from a context you hold, without reading the ambient one.

The scope state, `CorrelationLogScope`, is an `IReadOnlyList<KeyValuePair<string, object>>` with the id
under `CorrelationId` (`CorrelationLogScope.CorrelationIdKey`) and each selected baggage key that is
present; its `ToString()` renders `Key:Value` pairs for providers that format a scope as text.

---

## Configuration: `CorrelationOptions`

`Moongazing.OrionLens.CorrelationOptions` holds the header names and the policy flags.

| Option                  | Default            | Effect                                                                                 |
|-------------------------|--------------------|----------------------------------------------------------------------------------------|
| `CorrelationHeader`     | `X-Correlation-ID` | Header carrying the correlation id, read inbound and written outbound.                  |
| `BaggageHeader`         | `X-Orion-Baggage`  | Header carrying percent-encoded `key=value` baggage pairs joined by commas.             |
| `GenerateIdWhenMissing` | `true`             | When true, mint a new id for an inbound request that has none (and no trace-id to adopt). |
| `MissingIdSentinel`     | `""`               | The id used when `GenerateIdWhenMissing` is false and none arrives, taken verbatim.     |
| `WriteResponseHeader`   | `true`             | When true, the ASP.NET Core middleware echoes the id back on the response.             |
| `UseTraceContext`       | `false`            | Read and write `traceparent`; the middleware uses a trace-linked scope.                 |
| `TraceParentHeader`     | `traceparent`      | The W3C trace-context header name.                                                      |
| `AlignWithActivity`     | `false`            | Seed an absent id and the sampling decision from `Activity.Current`; the middleware tags it. |
| `ActivityCorrelationTag` | `orion.correlation_id` | Tag key for the id on the current `Activity`.                                      |
| `ActivityBaggageKeys`   | empty              | Baggage keys copied onto the current `Activity`'s baggage.                              |
| `UseW3CBaggage`         | `false`            | Also read and write the standard W3C `baggage` header.                                  |
| `W3CBaggageHeader`      | `baggage`          | The W3C baggage header name.                                                            |
| `SampledOnlyBaggageKeys` | empty             | Baggage keys written on inject only when the context is sampled.                        |
| `MaxBaggageCount`       | `null`             | Cap on baggage pairs crossing a boundary; `null` is no cap.                             |
| `MaxBaggageBytes`       | `null`             | Cap on the encoded baggage header size in bytes; `null` is no cap.                      |
| `NonPropagatingBaggageKeys` | empty          | Keys accepted on extract but never written on inject.                                   |
| `LoggedBaggageKeys`     | empty              | Baggage keys the logging helpers add to the log scope.                                  |

The key sets compare ordinally. Options are validated when they are registered: the header names and
`ActivityCorrelationTag` must be non-empty, `MissingIdSentinel` non-null, and the two caps greater
than zero when set, so a bad configuration fails at startup rather than on the first request.

---

## ASP.NET Core middleware: `CorrelationMiddleware` and `UseOrionLens()`

`Moongazing.OrionLens.AspNetCore.CorrelationMiddleware` establishes the ambient context for each
request. On every request it:

1. Extracts the inbound id and baggage via `CorrelationPropagator.Extract`, minting an id when one is
   missing (subject to `GenerateIdWhenMissing`).
2. Opens an `OrionContext` scope for the duration of the inner pipeline (through
   `OrionTraceContextScope.BeginTraceLinkedScope` when `UseTraceContext` is set, which can reconcile
   the id to a current `Activity`'s trace-id) and restores the previous context when the request
   completes.
3. When `AlignWithActivity` is set, writes the id and `ActivityBaggageKeys` onto `Activity.Current`.
4. When `WriteResponseHeader` is set, writes the now-current id onto the response header before the
   body starts, so the caller sees the id even on an error response.

Register it with `UseOrionLens()` early in the pipeline, before anything that logs or makes
downstream calls.

---

## Outbound handler: `CorrelationPropagationHandler`

`Moongazing.OrionLens.Http.CorrelationPropagationHandler` is a `DelegatingHandler` that injects the
ambient context into every outbound request, so the id and baggage established at the edge follow
calls to downstream services.

It reads `OrionContext.Current`; if there is a context, it injects the id and baggage onto the
outgoing request (removing any pre-existing header of the same name first so the value is not
duplicated). If there is no ambient context, the request goes out unchanged with no correlation
headers. Attach it to a named or typed `HttpClient` with `.AddHttpMessageHandler<CorrelationPropagationHandler>()`.

---

## Dependency injection: `AddOrionLens()`

`Moongazing.OrionLens.OrionLensServiceCollectionExtensions` provides the registration and pipeline
helpers.

- **`AddOrionLens(this IServiceCollection, Action<CorrelationOptions>? configure = null)`** - builds
  a `CorrelationOptions`, applies the optional configuration, validates it eagerly, registers the
  options as a singleton (via `TryAddSingleton`), and registers `CorrelationPropagationHandler` as
  transient (via `TryAddTransient`). The `TryAdd` calls make the registration idempotent.
- **`UseOrionLens(this IApplicationBuilder)`** - adds `CorrelationMiddleware` to the pipeline.

---

## Target frameworks and build

OrionLens multi-targets `net8.0`, `net9.0`, and `net10.0`. The build runs with nullable reference
types enabled, implicit usings, latest-recommended analysis, and warnings treated as errors, and it
generates an XML documentation file. There are no third-party runtime dependencies; the package
references only the ASP.NET Core shared framework (`Microsoft.AspNetCore.App`). A NativeAOT smoke
test (`tests/Moongazing.OrionLens.AotSmoke`) is published in CI with zero trim/AOT warnings.
