namespace Moongazing.OrionLens;

/// <summary>
/// Configuration for correlation propagation: the header names carrying the id and baggage, and
/// whether to mint a new id when an inbound request has none.
/// </summary>
public sealed class CorrelationOptions
{
    /// <summary>The header carrying the correlation id. Default <c>X-Correlation-ID</c>.</summary>
    public string CorrelationHeader { get; set; } = "X-Correlation-ID";

    /// <summary>
    /// The header carrying baggage as <c>key=value</c> pairs joined by commas, each key and value
    /// percent-encoded. Default <c>X-Orion-Baggage</c>.
    /// </summary>
    public string BaggageHeader { get; set; } = "X-Orion-Baggage";

    /// <summary>
    /// When true (the default), an inbound request without a correlation id is given a freshly
    /// generated one. When false, such a request gets an empty-baggage context whose id is
    /// <see cref="MissingIdSentinel"/> (empty by default, meaning the id is taken verbatim and may
    /// be empty).
    /// </summary>
    public bool GenerateIdWhenMissing { get; set; } = true;

    /// <summary>
    /// The id used for an inbound request that has no correlation id when
    /// <see cref="GenerateIdWhenMissing"/> is false. Defaults to <see cref="string.Empty"/>, so the
    /// missing id is taken verbatim (an empty id) rather than being replaced by an invented value.
    /// Set this to a sentinel such as <c>"unknown"</c> if you would rather log against a non-empty
    /// placeholder.
    /// </summary>
    public string MissingIdSentinel { get; set; } = string.Empty;

    /// <summary>
    /// When true (the default), the correlation id is echoed back on the response so the caller can
    /// record it. Used by the ASP.NET Core middleware.
    /// </summary>
    public bool WriteResponseHeader { get; set; } = true;

    /// <summary>
    /// When true, OrionLens bridges the correlation id to the W3C trace context. On extract, a
    /// <see cref="TraceParentHeader"/> is read and, when no explicit correlation id is present, its
    /// 32-hex trace-id becomes the correlation id. On inject, a <c>traceparent</c> is emitted that
    /// carries the current <see cref="System.Diagnostics.Activity"/> (or, failing that, a trace-id
    /// derived from the correlation id) so the correlation id and the W3C trace id line up across
    /// systems. Defaults to false, so behaviour is unchanged unless you opt in.
    /// </summary>
    public bool UseTraceContext { get; set; }

    /// <summary>
    /// The header carrying the W3C trace context, read on extract and written on inject when
    /// <see cref="UseTraceContext"/> is set. Default <c>traceparent</c> per the W3C spec.
    /// </summary>
    public string TraceParentHeader { get; set; } = "traceparent";

    /// <summary>
    /// The maximum number of baggage pairs allowed to cross a propagation boundary, or null (the
    /// default) for no limit. The cap is enforced on both inbound extract and outbound inject: once
    /// the limit is reached, further pairs are dropped (not propagated) rather than throwing, so a
    /// policy breach never fails a live request. Pairs are kept in ordinal key order, so the dropped
    /// set is deterministic. Must be greater than zero when set.
    /// </summary>
    public int? MaxBaggageCount { get; set; }

    /// <summary>
    /// The maximum total size, in bytes, of the encoded baggage header value (the percent-encoded
    /// <c>key=value</c> pairs joined by commas) allowed to cross a propagation boundary, or null (the
    /// default) for no limit. As with <see cref="MaxBaggageCount"/>, the cap is enforced on extract
    /// and inject by dropping the pairs that would push the encoded value past the limit, in ordinal
    /// key order, rather than throwing. Must be greater than zero when set.
    /// </summary>
    public int? MaxBaggageBytes { get; set; }

    /// <summary>
    /// Baggage keys marked as inbound-only / non-propagating. Such a key is still accepted on inbound
    /// <see cref="Context.CorrelationPropagator.Extract"/> (so internal code can read it), but it is
    /// never written on outbound <see cref="Context.CorrelationPropagator.Inject"/>, so an internal
    /// value is not leaked across a trust boundary to a downstream service. Comparison is ordinal,
    /// matching baggage key comparison elsewhere. Empty by default (every key propagates).
    /// </summary>
    public ISet<string> NonPropagatingBaggageKeys { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// The baggage keys (besides the correlation id, which is always included) that the logging
    /// enrichment helpers push into a logging scope. Selecting keys explicitly, rather than emitting
    /// all baggage, keeps internal or high-cardinality values out of logs unless you opt them in.
    /// Comparison is ordinal. Empty by default, so only the correlation id is enriched.
    /// </summary>
    public ISet<string> LoggedBaggageKeys { get; } = new HashSet<string>(StringComparer.Ordinal);

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrEmpty(CorrelationHeader);
        ArgumentException.ThrowIfNullOrEmpty(BaggageHeader);
        ArgumentNullException.ThrowIfNull(MissingIdSentinel);
        ArgumentException.ThrowIfNullOrEmpty(TraceParentHeader);

        if (MaxBaggageCount is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxBaggageCount), MaxBaggageCount, "MaxBaggageCount must be greater than zero when set.");
        }

        if (MaxBaggageBytes is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxBaggageBytes), MaxBaggageBytes, "MaxBaggageBytes must be greater than zero when set.");
        }
    }

    /// <summary>
    /// Whether any baggage policy (a count or size cap, or a non-propagating key) is configured. When
    /// false, outbound formatting takes a fast path that emits every pair in the original order with no
    /// extra allocation, so the no-policy case keeps its prior wire output and cost exactly.
    /// </summary>
    internal bool HasBaggagePolicy =>
        MaxBaggageCount is not null || MaxBaggageBytes is not null || NonPropagatingBaggageKeys.Count > 0;
}
