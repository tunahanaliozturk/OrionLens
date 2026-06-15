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
    /// generated one. When false, such a request gets an empty-baggage context with the id taken
    /// verbatim (which may be empty).
    /// </summary>
    public bool GenerateIdWhenMissing { get; set; } = true;

    /// <summary>
    /// When true (the default), the correlation id is echoed back on the response so the caller can
    /// record it. Used by the ASP.NET Core middleware.
    /// </summary>
    public bool WriteResponseHeader { get; set; } = true;

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrEmpty(CorrelationHeader);
        ArgumentException.ThrowIfNullOrEmpty(BaggageHeader);
    }
}
