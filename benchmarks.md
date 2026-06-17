# Benchmarks

Micro-benchmarks for OrionLens, built with [BenchmarkDotNet](https://benchmarkdotnet.org/). They
cover the in-memory hot paths only: the immutable context data model, the HTTP-boundary header
propagation, and the `AsyncLocal`-backed ambient scope. Nothing here touches a network, a database,
or any external service.

The project lives in `benchmarks/Moongazing.OrionLens.Benchmarks` and references the library
directly, so every benchmark exercises the real public API.

## What is measured

### `CorrelationContextBenchmarks`
The immutable context model in `Moongazing.OrionLens.Context.CorrelationContext`.

- `Create_NoBaggage` (baseline) and `Create_WithBaggage` - building a context, the second seeding a
  `FrozenDictionary` from three baggage pairs.
- `WithBaggage_AddOne` - the copy-on-write cost of adding one pair, which rebuilds the frozen
  dictionary. This is the per-scope allocation when nesting baggage.
- `GetBaggage_Hit` / `GetBaggage_Miss` - frozen-dictionary lookups for a present and an absent key.

### `CorrelationPropagatorBenchmarks`
The HTTP-boundary work in `Moongazing.OrionLens.Context.CorrelationPropagator`.

- `Extract_WithBaggage` - parse inbound id and baggage headers into a context, percent-decoding each
  pair. Runs on every inbound request.
- `Inject_WithBaggage` - format a context's id and baggage back into outbound headers,
  percent-encoding each pair. Runs on every downstream call.

### `OrionContextBenchmarks`
The ambient-scope primitives in `Moongazing.OrionLens.Context.OrionContext`.

- `ReadCurrent` - open a scope and read `Current` once.
- `BeginScope_Dispose` - the establish-then-restore round trip of a single scope over `AsyncLocal`.
- `BeginScope_Nested` - a nested scope with added baggage, restoring to the outer context on dispose.

## Running

Run the whole suite (from the repository root):

```
dotnet run -c Release --project benchmarks/Moongazing.OrionLens.Benchmarks
```

Filter to one class or one benchmark:

```
dotnet run -c Release --project benchmarks/Moongazing.OrionLens.Benchmarks -- --filter "*CorrelationPropagator*"
dotnet run -c Release --project benchmarks/Moongazing.OrionLens.Benchmarks -- --filter "*Extract_WithBaggage*"
```

List everything without running:

```
dotnet run -c Release --project benchmarks/Moongazing.OrionLens.Benchmarks -- --list flat
```

Each class carries `[MemoryDiagnoser]` and runs under both `net8.0` and `net9.0` jobs, so each row
reports time and allocations side by side across the two runtimes. Run on a quiet machine for stable
numbers; results are written under `BenchmarkDotNet.Artifacts`.

No measured numbers are committed here. Treat the benchmarks as the source of truth and run them on
your own hardware.
