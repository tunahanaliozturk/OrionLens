namespace Moongazing.OrionLens.Context;

using System.Diagnostics;
using System.Text;

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
            // No inbound correlation id. Prefer the current recording Activity's trace-id (when
            // AlignWithActivity is on), then an inbound W3C trace, then minting, then the configured
            // sentinel (empty by default) taken verbatim as documented.
            var activityTraceId = options.AlignWithActivity
                ? TryGetCurrentActivityTraceId()
                : null;
            var traceId = activityTraceId ?? (options.UseTraceContext
                ? W3CTraceContext.TryGetTraceId(getHeader(options.TraceParentHeader))
                : null);

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

        // Carry the inbound sampling decision onto the context so SampledOnlyBaggageKeys can react and
        // a derived traceparent reflects it. Prefer the current Activity's recorded flag (head-based
        // sampling already decided locally), then the inbound traceparent flag. When neither is known
        // the context stays sampled (the behaviour-compatible default); we never force a span.
        if (options.AlignWithActivity || options.UseTraceContext)
        {
            var sampled = (options.AlignWithActivity ? TryGetCurrentActivitySampled() : null)
                ?? (options.UseTraceContext
                    ? W3CTraceContext.TryGetSampledFlag(getHeader(options.TraceParentHeader))
                    : null);
            if (sampled is { } decision)
            {
                context = context.WithSampled(decision);
            }
        }

        context = ApplyInboundBaggage(getHeader(options.BaggageHeader), context, options);

        // When W3C baggage interop is on, also admit the standard baggage header. The custom header is
        // applied first and wins on a key collision (it is the OrionLens-native channel).
        if (options.UseW3CBaggage
            && !string.Equals(options.W3CBaggageHeader, options.BaggageHeader, StringComparison.Ordinal))
        {
            context = ApplyInboundBaggage(getHeader(options.W3CBaggageHeader), context, options, skipExisting: true);
        }

        return context;
    }

    private static CorrelationContext ApplyInboundBaggage(
        string? header, CorrelationContext context, CorrelationOptions options, bool skipExisting = false)
    {
        if (string.IsNullOrEmpty(header))
        {
            return context;
        }

        foreach (var (key, value) in ParseBaggage(header, options))
        {
            if (skipExisting && context.GetBaggage(key) is not null)
            {
                continue;
            }

            context = context.WithBaggage(key, value);
        }

        return context;
    }

    private static string? TryGetCurrentActivityTraceId()
    {
        var activity = Activity.Current;
        if (activity is not { IdFormat: ActivityIdFormat.W3C })
        {
            return null;
        }

        var traceId = activity.TraceId.ToHexString();
        return traceId.Length == 32 && traceId.AsSpan().IndexOfAnyExcept('0') >= 0 ? traceId : null;
    }

    private static bool? TryGetCurrentActivitySampled()
    {
        var activity = Activity.Current;
        return activity is null
            ? null
            : (activity.ActivityTraceFlags & ActivityTraceFlags.Recorded) != 0;
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
            var formatted = FormatBaggage(context.Baggage, options, context.IsSampled);
            if (!string.IsNullOrEmpty(formatted))
            {
                setHeader(options.BaggageHeader, formatted);

                // When W3C baggage interop is on, emit the same policy-filtered payload on the
                // standard baggage header too, so a system that speaks only W3C baggage still receives
                // it. The wire encoding is the same key=value comma-joined, percent-encoded form.
                if (options.UseW3CBaggage
                    && !string.Equals(options.W3CBaggageHeader, options.BaggageHeader, StringComparison.Ordinal))
                {
                    setHeader(options.W3CBaggageHeader, formatted);
                }
            }
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

            var traceParent = W3CTraceContext.Format(context.CorrelationId, aligned, derivedTraceId, context.IsSampled);
            if (traceParent is not null)
            {
                setHeader(options.TraceParentHeader, traceParent);
            }
        }
    }

    /// <summary>
    /// Encode the baggage for the outbound header, applying the configured policy: non-propagating
    /// keys are dropped, and the count and byte caps bound what is emitted. Pairs are considered in
    /// ordinal key order so the kept set is deterministic when a cap drops some of them. Returns an
    /// empty string when nothing survives the policy, in which case the header is omitted.
    /// </summary>
    private static string FormatBaggage(
        IReadOnlyDictionary<string, string> baggage, CorrelationOptions options, bool isSampled)
    {
        if (!options.HasBaggagePolicy)
        {
            // Fast path: no policy configured, so emit every pair exactly as prior versions did
            // (dictionary order, no per-pair caps), with no extra allocation or sorting.
            return FormatBaggageUnfiltered(baggage);
        }

        var nonPropagating = options.NonPropagatingBaggageKeys;
        var sampledOnly = options.SampledOnlyBaggageKeys;
        var maxCount = options.MaxBaggageCount;
        var maxBytes = options.MaxBaggageBytes;

        // Order by key so the policy keeps a stable, predictable subset under a cap rather than
        // depending on dictionary enumeration order.
        var keys = new List<string>(baggage.Count);
        foreach (var key in baggage.Keys)
        {
            if (nonPropagating.Count > 0 && nonPropagating.Contains(key))
            {
                continue;
            }

            // Sampled-only keys ride only a recorded trace; on an unsampled context they are dropped
            // so heavy diagnostic baggage does not travel with traces a backend will discard.
            if (!isSampled && sampledOnly.Count > 0 && sampledOnly.Contains(key))
            {
                continue;
            }

            keys.Add(key);
        }

        keys.Sort(StringComparer.Ordinal);

        var builder = new StringBuilder();
        var emitted = 0;
        foreach (var key in keys)
        {
            if (maxCount is { } countLimit && emitted >= countLimit)
            {
                break;
            }

            var encoded = $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(baggage[key])}";
            var addedLength = emitted == 0 ? encoded.Length : encoded.Length + 1; // +1 for the comma separator.

            if (maxBytes is { } byteLimit && builder.Length + addedLength > byteLimit)
            {
                // Skip this pair (it would push the encoded header past the byte cap) but keep trying
                // later, smaller pairs that may still fit.
                continue;
            }

            if (emitted > 0)
            {
                builder.Append(',');
            }

            builder.Append(encoded);
            emitted++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Encode every baggage pair with no policy applied, in dictionary enumeration order, matching the
    /// pre-policy behaviour exactly. Used on the no-policy fast path so the common case keeps its prior
    /// wire output and allocation profile.
    /// </summary>
    private static string FormatBaggageUnfiltered(IReadOnlyDictionary<string, string> baggage)
    {
        var parts = new List<string>(baggage.Count);
        foreach (var (key, value) in baggage)
        {
            parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }

        return string.Join(',', parts);
    }

    /// <summary>
    /// Parse the inbound baggage header, applying the count cap (the byte cap is bounded by the
    /// inbound header length, which is already finite). Inbound-only / non-propagating marks do not
    /// apply on extract: such keys are accepted inbound and only suppressed on inject.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string>> ParseBaggage(string header, CorrelationOptions options)
    {
        var maxCount = options.MaxBaggageCount;
        var maxBytes = options.MaxBaggageBytes;
        var emitted = 0;
        var bytes = 0;

        foreach (var pair in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (maxCount is { } countLimit && emitted >= countLimit)
            {
                yield break;
            }

            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            // Measure against the encoded (wire) length of the pair so the inbound byte cap matches
            // the same accounting the outbound side uses.
            if (maxBytes is { } byteLimit)
            {
                var addedLength = emitted == 0 ? pair.Length : pair.Length + 1; // +1 for the comma separator.
                if (bytes + addedLength > byteLimit)
                {
                    yield break;
                }

                bytes += addedLength;
            }

            var key = Uri.UnescapeDataString(pair[..separator]);
            var value = Uri.UnescapeDataString(pair[(separator + 1)..]);
            if (!string.IsNullOrEmpty(key))
            {
                emitted++;
                yield return new KeyValuePair<string, string>(key, value);
            }
        }
    }
}
