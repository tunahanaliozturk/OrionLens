namespace Moongazing.OrionLens.Logging;

using System.Text;

using Moongazing.OrionLens.Context;

/// <summary>
/// The state object pushed into an <see cref="Microsoft.Extensions.Logging.ILogger"/> scope by the
/// OrionLens logging enrichment helpers. It carries the correlation id and the selected baggage as a
/// read-only list of name/value pairs, which is the shape structured logging providers (the console
/// JSON formatter, Serilog, and others) enumerate to lift each pair into a named log property. Its
/// <see cref="ToString"/> renders the same data as text for providers that render a scope as a
/// single string.
/// </summary>
/// <remarks>
/// The state is immutable: it is built once from a <see cref="CorrelationContext"/> snapshot when the
/// scope is opened. The correlation id is always present under <see cref="CorrelationIdKey"/>; each
/// selected baggage value is present under its own key.
/// </remarks>
public sealed class CorrelationLogScope : IReadOnlyList<KeyValuePair<string, object>>
{
    /// <summary>The scope property name carrying the correlation id. Value <c>CorrelationId</c>.</summary>
    public const string CorrelationIdKey = "CorrelationId";

    private readonly KeyValuePair<string, object>[] values;
    private string? cachedToString;

    private CorrelationLogScope(KeyValuePair<string, object>[] values) => this.values = values;

    /// <inheritdoc />
    public int Count => values.Length;

    /// <inheritdoc />
    public KeyValuePair<string, object> this[int index] => values[index];

    /// <summary>
    /// Build a scope state from a context, including the correlation id and the baggage keys named in
    /// <paramref name="loggedBaggageKeys"/> that are present on the context. A null or empty key set
    /// yields the correlation id alone. A baggage key that is not present on the context is skipped
    /// (no null-valued property is emitted).
    /// </summary>
    /// <param name="context">The context to read the id and baggage from.</param>
    /// <param name="loggedBaggageKeys">The baggage keys to include, or null for none.</param>
    internal static CorrelationLogScope FromContext(
        CorrelationContext context, IEnumerable<string>? loggedBaggageKeys)
    {
        ArgumentNullException.ThrowIfNull(context);

        var buffer = new List<KeyValuePair<string, object>>
        {
            new(CorrelationIdKey, context.CorrelationId),
        };

        if (loggedBaggageKeys is not null)
        {
            foreach (var key in loggedBaggageKeys)
            {
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                var value = context.GetBaggage(key);
                if (value is not null)
                {
                    buffer.Add(new KeyValuePair<string, object>(key, value));
                }
            }
        }

        return new CorrelationLogScope(buffer.ToArray());
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        foreach (var pair in values)
        {
            yield return pair;
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Render the scope as <c>Key:Value</c> pairs joined by spaces, for providers that format a scope
    /// as a single string rather than enumerating its pairs. Computed once and cached.
    /// </summary>
    public override string ToString()
    {
        if (cachedToString is not null)
        {
            return cachedToString;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(values[i].Key).Append(':').Append(values[i].Value);
        }

        cachedToString = builder.ToString();
        return cachedToString;
    }
}
