namespace Moongazing.OrionLens.Tests;

using System.Diagnostics;

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
    public void Extract_takes_the_missing_id_verbatim_as_empty_when_generation_is_off()
    {
        // Documented behaviour: with generation off and no inbound id, the id is taken verbatim,
        // which is empty by default (MissingIdSentinel defaults to string.Empty). It is no longer
        // replaced by an invented "unknown".
        var options = new CorrelationOptions { GenerateIdWhenMissing = false };
        var context = CorrelationPropagator.Extract(_ => null, options);

        Assert.Equal(string.Empty, context.CorrelationId);
    }

    [Fact]
    public void Extract_uses_a_configured_sentinel_when_generation_is_off()
    {
        var options = new CorrelationOptions { GenerateIdWhenMissing = false, MissingIdSentinel = "unknown" };
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

    [Fact]
    public void Inject_traceparent_carries_the_correlation_id()
    {
        var options = new CorrelationOptions { UseTraceContext = true };
        var headers = new Dictionary<string, string>();
        // A 32-hex correlation id is used verbatim as the W3C trace-id.
        const string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";

        CorrelationPropagator.Inject(CorrelationContext.Create(traceId), (k, v) => headers[k] = v, options);

        Assert.True(headers.TryGetValue(options.TraceParentHeader, out var traceParent));
        Assert.Contains(traceId, traceParent!, StringComparison.Ordinal);
        // The traceparent's trace-id and the X-Correlation-ID must agree.
        Assert.Equal(traceId, headers[options.CorrelationHeader]);
    }

    [Fact]
    public void Inject_does_not_adopt_an_unrelated_ambient_activity_trace_id()
    {
        var options = new CorrelationOptions { UseTraceContext = true };

        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var source = new ActivitySource("Moongazing.OrionLens.Tests.Trace");
        using var activity = source.StartActivity("unrelated");
        Assert.NotNull(activity);
        Assert.Equal(ActivityIdFormat.W3C, activity!.IdFormat);

        const string correlationId = "4bf92f3577b34da6a3ce929d0e0e4736";
        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(CorrelationContext.Create(correlationId), (k, v) => headers[k] = v, options);

        var traceParent = headers[options.TraceParentHeader];
        // The emitted trace-id must track the correlation id, not the unrelated ambient Activity, so
        // a downstream service never sees traceparent and X-Correlation-ID disagree.
        Assert.Contains(correlationId, traceParent, StringComparison.Ordinal);
        Assert.DoesNotContain(activity.TraceId.ToHexString(), traceParent, StringComparison.Ordinal);
    }
}
