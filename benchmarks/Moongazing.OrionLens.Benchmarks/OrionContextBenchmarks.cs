namespace Moongazing.OrionLens.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Moongazing.OrionLens.Context;

/// <summary>
/// Measures the ambient-context primitives backed by <c>AsyncLocal</c>: reading <c>Current</c> and
/// the establish-then-restore cost of a nested scope. These run wherever code enriches logs or an
/// entry point opens a correlation scope.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class OrionContextBenchmarks
{
    private CorrelationContext context = default!;

    [GlobalSetup]
    public void Setup()
    {
        context = CorrelationContext.Create("9f8e7d6c5b4a39281706f5e4d3c2b1a0");
    }

    [Benchmark]
    public CorrelationContext? ReadCurrent()
    {
        using (OrionContext.BeginScope(context))
        {
            return OrionContext.Current;
        }
    }

    [Benchmark]
    public CorrelationContext? BeginScope_Dispose()
    {
        using var scope = OrionContext.BeginScope(context);
        return OrionContext.Current;
    }

    [Benchmark]
    public CorrelationContext? BeginScope_Nested()
    {
        using var outer = OrionContext.BeginScope(context);
        using (OrionContext.BeginScope(context.WithBaggage("depth", "1")))
        {
            return OrionContext.Current;
        }
    }
}
