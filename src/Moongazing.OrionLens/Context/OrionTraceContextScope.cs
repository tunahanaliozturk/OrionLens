namespace Moongazing.OrionLens.Context;

using System.Diagnostics;

/// <summary>
/// Opt-in helpers that begin an ambient correlation scope linked to the W3C trace context, so the
/// correlation id and the current <see cref="Activity"/> trace-id refer to the same logical trace.
/// Use these when you want OrionLens correlation and OpenTelemetry/<see cref="Activity"/> tracing to
/// line up; the plain <see cref="OrionContext.BeginScope(CorrelationContext)"/> leaves
/// <see cref="Activity"/> untouched.
/// </summary>
public static class OrionTraceContextScope
{
    /// <summary>
    /// The <see cref="ActivitySource"/> OrionLens starts trace-linked activities from. Add it to a
    /// listener or an OpenTelemetry <c>AddSource</c> call to record these activities.
    /// </summary>
    public static readonly ActivitySource Source = new("Moongazing.OrionLens");

    /// <summary>
    /// Begin an ambient scope for <paramref name="context"/> that is aligned with the W3C trace
    /// context. When a W3C <see cref="Activity"/> is already current, the context's correlation id is
    /// reconciled to that activity's trace-id (so logs keyed on either id agree). When none is
    /// current, a new activity is started whose trace-id is derived from the correlation id. The
    /// returned scope restores the previous ambient context and stops any activity it started.
    /// </summary>
    /// <param name="context">The context to make current.</param>
    /// <param name="activityName">The name for any activity started. Defaults to <c>"orion.scope"</c>.</param>
    public static IDisposable BeginTraceLinkedScope(CorrelationContext context, string activityName = "orion.scope")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(activityName);

        var current = Activity.Current;
        if (current is not null && current.IdFormat == ActivityIdFormat.W3C)
        {
            // An activity is already running: adopt its trace-id as the correlation id so the two
            // identifiers agree, then begin the ambient scope without owning an activity.
            var aligned = AlignTo(context, current.TraceId.ToHexString());
            return OrionContext.BeginScope(aligned);
        }

        // No W3C activity: start one whose trace-id derives from the correlation id, so the emitted
        // trace and the correlation id share an identifier.
        var traceId = W3CTraceContext.ToTraceId(context.CorrelationId);
        Activity? started;
        if (traceId is not null)
        {
            var traceContext = new ActivityContext(
                ActivityTraceId.CreateFromString(traceId.AsSpan()),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None);
            started = Source.StartActivity(activityName, ActivityKind.Internal, traceContext);
        }
        else
        {
            started = Source.StartActivity(activityName);
        }

        var ambient = OrionContext.BeginScope(context);
        return new LinkedScope(ambient, started);
    }

    /// <summary>
    /// Write the correlation id (as a tag named <paramref name="correlationTag"/>) and the selected
    /// baggage keys onto the current <see cref="Activity"/>, so an <see cref="Activity"/>-based tracer
    /// surfaces the same identifier OrionLens carries. Does nothing when no activity is current or the
    /// id is empty; it never starts a span. A baggage key already present on the activity is left as-is
    /// so an inbound activity baggage value is not overwritten.
    /// </summary>
    /// <param name="context">The context whose id and baggage to project onto the activity.</param>
    /// <param name="correlationTag">The tag key for the correlation id.</param>
    /// <param name="baggageKeys">The baggage keys to copy onto the activity, or null for none.</param>
    public static void AlignCurrentActivity(
        CorrelationContext context, string correlationTag, ISet<string>? baggageKeys = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(correlationTag);

        var activity = Activity.Current;
        if (activity is null || string.IsNullOrEmpty(context.CorrelationId))
        {
            return;
        }

        activity.SetTag(correlationTag, context.CorrelationId);

        if (baggageKeys is null || baggageKeys.Count == 0)
        {
            return;
        }

        foreach (var key in baggageKeys)
        {
            var value = context.GetBaggage(key);
            if (value is not null && activity.GetBaggageItem(key) is null)
            {
                activity.SetBaggage(key, value);
            }
        }
    }

    private static CorrelationContext AlignTo(CorrelationContext context, string traceId)
    {
        if (string.Equals(context.CorrelationId, traceId, StringComparison.Ordinal))
        {
            return context;
        }

        var aligned = CorrelationContext.Create(traceId);
        foreach (var (key, value) in context.Baggage)
        {
            aligned = aligned.WithBaggage(key, value);
        }

        return aligned;
    }

    private sealed class LinkedScope : IDisposable
    {
        private readonly IDisposable ambient;
        private readonly Activity? activity;
        private int disposed;

        public LinkedScope(IDisposable ambient, Activity? activity)
        {
            this.ambient = ambient;
            this.activity = activity;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            // Restore the ambient context first, then stop the activity we started (if any).
            ambient.Dispose();
            activity?.Dispose();
        }
    }
}
