namespace Moongazing.OrionLens.Tests;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;

using Xunit;

public sealed class CorrelationPropagatorTests
{
    private static readonly CorrelationOptions Options = new();

    [Fact]
    public void Extract_uses_the_inbound_id()
    {
        var headers = new Dictionary<string, string> { ["X-Correlation-ID"] = "given-id" };
        var context = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.Equal("given-id", context.CorrelationId);
    }

    [Fact]
    public void Extract_generates_an_id_when_missing()
    {
        var context = CorrelationPropagator.Extract(_ => null, Options);
        Assert.False(string.IsNullOrEmpty(context.CorrelationId));
    }

    [Fact]
    public void Extract_falls_back_to_a_sentinel_when_generation_is_off()
    {
        var options = new CorrelationOptions { GenerateIdWhenMissing = false };
        var context = CorrelationPropagator.Extract(_ => null, options);

        Assert.Equal("unknown", context.CorrelationId);
    }

    [Fact]
    public void Baggage_round_trips_through_inject_and_extract()
    {
        var source = CorrelationContext.Create("id-1")
            .WithBaggage("tenant", "acme")
            .WithBaggage("region", "eu-west");

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(source, (k, v) => headers[k] = v, Options);

        var roundTripped = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.Equal("id-1", roundTripped.CorrelationId);
        Assert.Equal("acme", roundTripped.GetBaggage("tenant"));
        Assert.Equal("eu-west", roundTripped.GetBaggage("region"));
    }

    [Fact]
    public void Baggage_values_with_separators_are_encoded_safely()
    {
        var source = CorrelationContext.Create("id-1").WithBaggage("note", "a,b=c");

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(source, (k, v) => headers[k] = v, Options);
        var roundTripped = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.Equal("a,b=c", roundTripped.GetBaggage("note"));
    }

    [Fact]
    public void Inject_omits_the_baggage_header_when_there_is_none()
    {
        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(CorrelationContext.Create("id-1"), (k, v) => headers[k] = v, Options);

        Assert.True(headers.ContainsKey("X-Correlation-ID"));
        Assert.False(headers.ContainsKey("X-Orion-Baggage"));
    }
}
