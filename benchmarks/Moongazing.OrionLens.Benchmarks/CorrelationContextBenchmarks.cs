namespace Moongazing.OrionLens.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Moongazing.OrionLens.Context;

/// <summary>
/// Measures the immutable context data model: creation, copy-on-write baggage addition, and lookup.
/// Every <see cref="CorrelationContext.WithBaggage"/> rebuilds a <c>FrozenDictionary</c>, so this is
/// the per-scope allocation cost that nesting baggage incurs.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class CorrelationContextBenchmarks
{
    private const string CorrelationId = "9f8e7d6c5b4a39281706f5e4d3c2b1a0";

    private static readonly Dictionary<string, string> SeedBaggage = new(StringComparer.Ordinal)
    {
        ["tenant"] = "acme",
        ["region"] = "eu-west-1",
        ["feature"] = "new-checkout",
    };

    private CorrelationContext context = default!;

    [GlobalSetup]
    public void Setup()
    {
        context = CorrelationContext.Create(CorrelationId, SeedBaggage);
    }

    [Benchmark(Baseline = true)]
    public CorrelationContext Create_NoBaggage()
    {
        return CorrelationContext.Create(CorrelationId);
    }

    [Benchmark]
    public CorrelationContext Create_WithBaggage()
    {
        return CorrelationContext.Create(CorrelationId, SeedBaggage);
    }

    [Benchmark]
    public CorrelationContext WithBaggage_AddOne()
    {
        return context.WithBaggage("request", "abc123");
    }

    [Benchmark]
    public string? GetBaggage_Hit()
    {
        return context.GetBaggage("region");
    }

    [Benchmark]
    public string? GetBaggage_Miss()
    {
        return context.GetBaggage("absent");
    }
}
