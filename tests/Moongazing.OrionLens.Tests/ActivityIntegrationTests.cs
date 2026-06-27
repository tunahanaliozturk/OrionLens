namespace Moongazing.OrionLens.Tests;

using System.Diagnostics;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;

using Xunit;

/// <summary>
/// v0.4.0: deeper Activity integration, sampling-aware correlation, and W3C baggage interop. The
/// Activity-dependent tests install an <see cref="ActivityListener"/> so a real W3C
/// <see cref="Activity"/> is created and assertions run against captured Activity / header state.
/// </summary>
public sealed class ActivityIntegrationTests
{
    private static ActivityListener Listen(ActivitySamplingResult sampling) =>
        new()
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => sampling,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => sampling,
        };

    [Fact]
    public void Extract_seeds_correlation_id_from_the_current_activity_when_aligned()
    {
        var options = new CorrelationOptions { AlignWithActivity = true };

        using var listener = Listen(ActivitySamplingResult.AllDataAndRecorded);
        ActivitySource.AddActivityListener(listener);
        using var source = new ActivitySource("Moongazing.OrionLens.Tests.Activity.Seed");
        using var activity = source.StartActivity("inbound");
        Assert.NotNull(activity);
        Assert.Equal(ActivityIdFormat.W3C, activity!.IdFormat);

        // No inbound id header: the correlation id must come from the current Activity's trace-id so
        // OrionLens and the Activity-based trace agree on the identifier.
        var context = CorrelationPropagator.Extract(_ => null, options);

        Assert.Equal(activity.TraceId.ToHexString(), context.CorrelationId);
    }

    [Fact]
    public void Align_writes_correlation_id_as_a_tag_on_the_current_activity()
    {
        using var listener = Listen(ActivitySamplingResult.AllDataAndRecorded);
        ActivitySource.AddActivityListener(listener);
        using var source = new ActivitySource("Moongazing.OrionLens.Tests.Activity.Tag");
        using var activity = source.StartActivity("op");
        Assert.NotNull(activity);

        var context = CorrelationContext.Create("corr-1").WithBaggage("tenant", "acme");
        var keys = new HashSet<string>(StringComparer.Ordinal) { "tenant" };

        OrionTraceContextScope.AlignCurrentActivity(context, "orion.correlation_id", keys);

        Assert.Equal("corr-1", activity!.GetTagItem("orion.correlation_id"));
        Assert.Equal("acme", activity.GetBaggageItem("tenant"));
    }

    [Fact]
    public void Align_is_a_no_op_when_no_activity_is_current()
    {
        // No listener, so StartActivity yields null and Activity.Current is null. The call must not
        // throw and must not force a span into existence.
        var context = CorrelationContext.Create("corr-1");

        OrionTraceContextScope.AlignCurrentActivity(context, "orion.correlation_id");

        Assert.Null(Activity.Current);
    }

    [Fact]
    public void Unsampled_context_still_carries_the_correlation_id_for_logging()
    {
        // Head-based sampling decided "not recorded". The correlation id must still propagate (logging
        // needs it on every request), only the sampling flag rides as not-sampled.
        var options = new CorrelationOptions { UseTraceContext = true };

        const string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        var inbound = new Dictionary<string, string>
        {
            ["traceparent"] = $"00-{traceId}-00f067aa0ba902b7-00",
        };

        var context = CorrelationPropagator.Extract(h => inbound.GetValueOrDefault(h), options);

        Assert.Equal(traceId, context.CorrelationId);
        Assert.False(context.IsSampled);
    }

    [Fact]
    public void Sampled_inbound_traceparent_marks_the_context_sampled()
    {
        var options = new CorrelationOptions { UseTraceContext = true };

        const string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        var inbound = new Dictionary<string, string>
        {
            ["traceparent"] = $"00-{traceId}-00f067aa0ba902b7-01",
        };

        var context = CorrelationPropagator.Extract(h => inbound.GetValueOrDefault(h), options);

        Assert.True(context.IsSampled);
    }

    [Fact]
    public void Extract_does_not_force_span_creation_under_sampling()
    {
        // No ActivityListener is installed: even with the bridge on, Extract must not create an
        // Activity. It reads an existing decision; it never starts one.
        var options = new CorrelationOptions { UseTraceContext = true, AlignWithActivity = true };

        var inbound = new Dictionary<string, string>
        {
            ["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00",
        };

        CorrelationPropagator.Extract(h => inbound.GetValueOrDefault(h), options);

        Assert.Null(Activity.Current);
    }

    [Fact]
    public void Unsampled_context_drops_sampled_only_baggage_on_inject()
    {
        var options = new CorrelationOptions();
        options.SampledOnlyBaggageKeys.Add("debug.dump");

        var context = CorrelationContext.Create("id-1")
            .WithBaggage("tenant", "acme")
            .WithBaggage("debug.dump", "heavy")
            .WithSampled(false);

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(context, (k, v) => headers[k] = v, options);

        var baggage = headers[options.BaggageHeader];
        Assert.Contains("tenant", baggage, StringComparison.Ordinal);
        // The heavy diagnostic key rides only sampled traces.
        Assert.DoesNotContain("debug.dump", baggage, StringComparison.Ordinal);
    }

    [Fact]
    public void Sampled_context_keeps_sampled_only_baggage_on_inject()
    {
        var options = new CorrelationOptions();
        options.SampledOnlyBaggageKeys.Add("debug.dump");

        var context = CorrelationContext.Create("id-1")
            .WithBaggage("debug.dump", "heavy");
        // Default IsSampled is true.

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(context, (k, v) => headers[k] = v, options);

        Assert.Contains("debug.dump", headers[options.BaggageHeader], StringComparison.Ordinal);
    }

    [Fact]
    public void Unsampled_context_emits_a_traceparent_with_the_not_recorded_flag()
    {
        var options = new CorrelationOptions { UseTraceContext = true };
        const string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";

        var context = CorrelationContext.Create(traceId).WithSampled(false);

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(context, (k, v) => headers[k] = v, options);

        // Derived traceparent must carry the trace-id and the 00 (not-recorded) flags.
        Assert.True(headers.TryGetValue("traceparent", out var traceParent));
        Assert.StartsWith($"00-{traceId}-", traceParent, StringComparison.Ordinal);
        Assert.EndsWith("-00", traceParent, StringComparison.Ordinal);
    }
}
