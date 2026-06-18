namespace Moongazing.OrionLens.Context;

using System.Collections.Frozen;

/// <summary>
/// An immutable snapshot of the ambient request context: the correlation id that ties together all
/// work for one logical operation, plus baggage (small key-value pairs carried alongside it, for
/// example a tenant id or a feature flag). Mutating helpers return a new instance.
/// </summary>
public sealed class CorrelationContext
{
    private readonly FrozenDictionary<string, string> baggage;

    private CorrelationContext(string correlationId, FrozenDictionary<string, string> baggage)
    {
        CorrelationId = correlationId;
        this.baggage = baggage;
    }

    /// <summary>The correlation id for the current logical operation.</summary>
    public string CorrelationId { get; }

    /// <summary>The baggage carried with the context.</summary>
    public IReadOnlyDictionary<string, string> Baggage => baggage;

    /// <summary>Create a context with a correlation id and no baggage.</summary>
    /// <param name="correlationId">The correlation id.</param>
    public static CorrelationContext Create(string correlationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(correlationId);
        return new CorrelationContext(correlationId, FrozenDictionary<string, string>.Empty);
    }

    /// <summary>
    /// Create a context with a correlation id that may be empty and no baggage. Used by the
    /// propagator to honour an inbound id "verbatim (which may be empty)" when id generation is off,
    /// without weakening the public <see cref="Create(string)"/> guard.
    /// </summary>
    /// <param name="correlationId">The correlation id, which may be empty but not null.</param>
    internal static CorrelationContext CreateAllowingEmpty(string correlationId)
    {
        ArgumentNullException.ThrowIfNull(correlationId);
        return new CorrelationContext(correlationId, FrozenDictionary<string, string>.Empty);
    }

    /// <summary>Create a context with a correlation id and a baggage set.</summary>
    /// <param name="correlationId">The correlation id.</param>
    /// <param name="baggage">The baggage pairs.</param>
    public static CorrelationContext Create(string correlationId, IReadOnlyDictionary<string, string> baggage)
    {
        ArgumentException.ThrowIfNullOrEmpty(correlationId);
        ArgumentNullException.ThrowIfNull(baggage);
        return new CorrelationContext(correlationId, baggage.ToFrozenDictionary(StringComparer.Ordinal));
    }

    /// <summary>Look up a baggage value, or null if the key is absent.</summary>
    /// <param name="key">The baggage key.</param>
    public string? GetBaggage(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return baggage.GetValueOrDefault(key);
    }

    /// <summary>Return a new context with one baggage pair added or replaced.</summary>
    /// <param name="key">The baggage key.</param>
    /// <param name="value">The baggage value.</param>
    public CorrelationContext WithBaggage(string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        var next = new Dictionary<string, string>(baggage, StringComparer.Ordinal)
        {
            [key] = value,
        };
        return new CorrelationContext(CorrelationId, next.ToFrozenDictionary(StringComparer.Ordinal));
    }
}
