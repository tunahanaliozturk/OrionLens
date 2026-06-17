# Features

A deeper look at every public type in OrionLens and how it behaves. Everything here is backed by the
source under `src/Moongazing.OrionLens` and exercised by the test suite under
`tests/Moongazing.OrionLens.Tests`.

The package id is `OrionLens`. The root namespace is `Moongazing.OrionLens`, with the context types
under `Moongazing.OrionLens.Context`, the middleware under `Moongazing.OrionLens.AspNetCore`, and the
outbound handler under `Moongazing.OrionLens.Http`.

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
  inbound headers. It reads the id header; when that is absent it mints a new id (a `Guid` in `N`
  format) if `GenerateIdWhenMissing` is set, and otherwise falls back to the sentinel `"unknown"` so
  the context always has a non-empty id to log against. It then reads the baggage header and adds
  each decoded pair.
- **`Inject(CorrelationContext context, Action<string, string> setHeader, CorrelationOptions options)`** -
  writes a context's id into the id header, and, when there is baggage, writes the baggage header.

Baggage on the wire is a comma-joined list of `key=value` pairs, with each key and value
percent-encoded (`Uri.EscapeDataString`) on the way out and decoded on the way in. Malformed pairs
(no `=`, or an empty key) are skipped on parse rather than throwing.

---

## Configuration: `CorrelationOptions`

`Moongazing.OrionLens.CorrelationOptions` holds the header names and the policy flags.

| Option                  | Default            | Effect                                                                                 |
|-------------------------|--------------------|----------------------------------------------------------------------------------------|
| `CorrelationHeader`     | `X-Correlation-ID` | Header carrying the correlation id, read inbound and written outbound.                  |
| `BaggageHeader`         | `X-Orion-Baggage`  | Header carrying percent-encoded `key=value` baggage pairs joined by commas.             |
| `GenerateIdWhenMissing` | `true`             | When true, mint a new id for an inbound request that has none.                          |
| `WriteResponseHeader`   | `true`             | When true, the ASP.NET Core middleware echoes the id back on the response.             |

The header names are validated (non-null, non-empty) when options are registered, so a bad
configuration fails at startup rather than on the first request.

---

## ASP.NET Core middleware: `CorrelationMiddleware` and `UseOrionLens()`

`Moongazing.OrionLens.AspNetCore.CorrelationMiddleware` establishes the ambient context for each
request. On every request it:

1. Extracts the inbound id and baggage via `CorrelationPropagator.Extract`, minting an id when one is
   missing (subject to `GenerateIdWhenMissing`).
2. When `WriteResponseHeader` is set, writes the id onto the response header before the body starts,
   so the caller sees the id even on an error response.
3. Opens an `OrionContext` scope for the duration of the inner pipeline and restores the previous
   context when the request completes.

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
generates an XML documentation file. There are no third-party runtime dependencies; the ASP.NET Core
surface references only the shared framework.
