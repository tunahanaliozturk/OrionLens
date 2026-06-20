namespace Moongazing.OrionLens.Context;

using System.Diagnostics;

using Moongazing.OrionLens;

/// <summary>
/// Extracts a <see cref="CorrelationContext"/> from inbound headers and injects one into outbound
/// headers, independent of any particular HTTP type. The header access is expressed as a getter and
/// a setter so the same logic serves an ASP.NET request, an <see cref="System.Net.Http.HttpClient"/>
/// request, or a message envelope.
/// </summary>
public static class CorrelationPropagator
{
    /// <summary>
    /// Build a context from inbound headers. When the id header is absent the id is, in order of
    /// preference: the inbound W3C trace-id (when <see cref="CorrelationOptions.UseTraceContext"/> is
    /// set and a valid <c>traceparent</c> is present), a freshly generated id (when
    /// <see cref="CorrelationOptions.GenerateIdWhenMissing"/> is set), or
    /// <see cref="CorrelationOptions.MissingIdSentinel"/> taken verbatim (empty by default).
    /// </summary>
    /// <param name="getHeader">Returns the value of a header, or null if absent.</param>
    /// <param name="options">The header names and generation policy.</param>
    public static CorrelationContext Extract(Func<string, string?> getHeader, CorrelationOptions options)
    {
        ArgumentNullException.ThrowIfNull(getHeader);
        ArgumentNullException.ThrowIfNull(options);

        var id = getHeader(options.CorrelationHeader);
        CorrelationContext context;
        if (string.IsNullOrEmpty(id))
        {
            // No inbound correlation id. Prefer aligning with an inbound W3C trace, then minting,
            // then the configured sentinel (empty by default) taken verbatim as documented.
            var traceId = options.UseTraceContext
                ? W3CTraceContext.TryGetTraceId(getHeader(options.TraceParentHeader))
                : null;

            if (traceId is not null)
            {
                context = CorrelationContext.Create(traceId);
            }
            else if (options.GenerateIdWhenMissing)
            {
                context = CorrelationContext.Create(Guid.NewGuid().ToString("N"));
            }
            else
            {
                // CreateAllowingEmpty honours an empty sentinel "verbatim (which may be empty)".
                context = CorrelationContext.CreateAllowingEmpty(options.MissingIdSentinel);
            }
        }
        else
        {
            context = CorrelationContext.Create(id);
        }

        var baggage = getHeader(options.BaggageHeader);
        if (!string.IsNullOrEmpty(baggage))
        {
            foreach (var (key, value) in ParseBaggage(baggage))
            {
                context = context.WithBaggage(key, value);
            }
        }

        return context;
    }

    /// <summary>Write a context's id and baggage into outbound headers.</summary>
    /// <param name="context">The context to propagate.</param>
    /// <param name="setHeader">Sets a header value.</param>
    /// <param name="options">The header names.</param>
    public static void Inject(CorrelationContext context, Action<string, string> setHeader, CorrelationOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(setHeader);
        ArgumentNullException.ThrowIfNull(options);

        setHeader(options.CorrelationHeader, context.CorrelationId);

        if (context.Baggage.Count > 0)
        {
            setHeader(options.BaggageHeader, FormatBaggage(context.Baggage));
        }

        if (options.UseTraceContext)
        {
            // Emit a traceparent whose trace-id is derived from the correlation id we just wrote, so a
            // downstream service never sees a W3C trace-id that conflicts with X-Correlation-ID. Reuse
            // the live Activity (for its span-id and flags) only when its trace-id already matches the
            // one this correlation id maps to; an unrelated ambient Activity is ignored.
            // Derive the trace-id once: it is needed both to test alignment with any ambient activity
            // and (when nothing aligns) as the emitted trace-id, so Format reuses it instead of
            // hashing the correlation id a second time.
            var derivedTraceId = W3CTraceContext.ToTraceId(context.CorrelationId);
            var activity = Activity.Current;
            var aligned = activity is { IdFormat: ActivityIdFormat.W3C }
                && string.Equals(
                    activity.TraceId.ToHexString(),
                    derivedTraceId,
                    StringComparison.Ordinal)
                ? activity
                : null;

            var traceParent = W3CTraceContext.Format(context.CorrelationId, aligned, derivedTraceId);
            if (traceParent is not null)
            {
                setHeader(options.TraceParentHeader, traceParent);
            }
        }
    }

    private static string FormatBaggage(IReadOnlyDictionary<string, string> baggage)
    {
        var parts = new List<string>(baggage.Count);
        foreach (var (key, value) in baggage)
        {
            parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }

        return string.Join(',', parts);
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseBaggage(string header)
    {
        foreach (var pair in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separator]);
            var value = Uri.UnescapeDataString(pair[(separator + 1)..]);
            if (!string.IsNullOrEmpty(key))
            {
                yield return new KeyValuePair<string, string>(key, value);
            }
        }
    }
}
