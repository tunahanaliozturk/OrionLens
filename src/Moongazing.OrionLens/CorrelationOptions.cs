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

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrEmpty(CorrelationHeader);
        ArgumentException.ThrowIfNullOrEmpty(BaggageHeader);
        ArgumentNullException.ThrowIfNull(MissingIdSentinel);
        ArgumentException.ThrowIfNullOrEmpty(TraceParentHeader);
    }
}
