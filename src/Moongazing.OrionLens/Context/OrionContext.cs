namespace Moongazing.OrionLens.Context;

/// <summary>
/// The ambient correlation context for the current async flow, backed by <see cref="AsyncLocal{T}"/>
/// so it follows <c>await</c> boundaries without being threaded through every method. Code reads
/// <see cref="Current"/> to enrich logs or propagate context; the entry point (a middleware, a
/// message consumer) establishes it with <see cref="BeginScope"/>.
/// </summary>
public static class OrionContext
{
    private static readonly AsyncLocal<CorrelationContext?> Ambient = new();

    /// <summary>The current context, or null when none has been established on this flow.</summary>
    public static CorrelationContext? Current => Ambient.Value;

    /// <summary>
    /// Set the ambient context for the current flow and return a scope that restores the previous
    /// context when disposed. Scopes nest: disposing restores exactly what was current before.
    /// </summary>
    /// <param name="context">The context to make current.</param>
    public static IDisposable BeginScope(CorrelationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = Ambient.Value;
        Ambient.Value = context;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly CorrelationContext? previous;
        private int disposed;

        public Scope(CorrelationContext? previous) => this.previous = previous;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                Ambient.Value = previous;
            }
        }
    }
}
