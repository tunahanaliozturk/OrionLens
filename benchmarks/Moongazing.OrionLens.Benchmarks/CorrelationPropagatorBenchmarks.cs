namespace Moongazing.OrionLens.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;

/// <summary>
/// Measures the HTTP-boundary header work: parsing inbound headers into a context (percent-decoding
/// each baggage pair) and formatting a context back into headers. This runs on every request in and
/// every downstream call out, so it is the propagation path's per-hop cost.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class CorrelationPropagatorBenchmarks
{
    private static readonly CorrelationOptions Options = new();

    private static readonly Dictionary<string, string?> InboundHeaders = new(StringComparer.Ordinal)
    {
        ["X-Correlation-ID"] = "9f8e7d6c5b4a39281706f5e4d3c2b1a0",
        ["X-Orion-Baggage"] = "tenant=acme,region=eu-west-1,feature=new%2Dcheckout",
    };

    private static readonly Func<string, string?> GetHeader =
        name => InboundHeaders.GetValueOrDefault(name);

    private CorrelationContext context = default!;

    [GlobalSetup]
    public void Setup()
    {
        context = CorrelationPropagator.Extract(GetHeader, Options);
    }

    [Benchmark]
    public CorrelationContext Extract_WithBaggage()
    {
        return CorrelationPropagator.Extract(GetHeader, Options);
    }

    [Benchmark]
    public int Inject_WithBaggage()
    {
        var written = 0;
        CorrelationPropagator.Inject(context, (_, _) => written++, Options);
        return written;
    }
}
