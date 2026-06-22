namespace Moongazing.OrionLens.Logging;

using Microsoft.Extensions.Logging;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;

/// <summary>
/// Opt-in helpers that push the current correlation id (and selected baggage) into an
/// <see cref="ILogger"/> scope, so structured logs carry it without a manual
/// <see cref="ILogger.BeginScope{TState}(TState)"/> call at every log site. The helpers depend only
/// on the logging abstractions (<see cref="ILogger"/> and its scope) and bind no specific logging
/// sink, so they work with any provider that honours scopes.
/// </summary>
/// <remarks>
/// Each helper returns the <see cref="IDisposable"/> from <see cref="ILogger.BeginScope{TState}(TState)"/>,
/// which ends the scope on dispose, or null when there is nothing to enrich (no ambient context, or a
/// provider that does not support scopes). Wrap the work whose logs should carry the id in a
/// <c>using</c> over the result.
/// </remarks>
public static class OrionLensLoggerExtensions
{
    /// <summary>
    /// Begin a logging scope carrying the ambient correlation id (from
    /// <see cref="OrionContext.Current"/>), or return null when no context is established on the
    /// current flow. No baggage is included; use the <see cref="CorrelationOptions"/> overload to
    /// include selected baggage keys.
    /// </summary>
    /// <param name="logger">The logger to open the scope on.</param>
    /// <returns>A disposable that ends the scope, or null when there is no ambient context.</returns>
    public static IDisposable? BeginCorrelationScope(this ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var context = OrionContext.Current;
        return context is null ? null : BeginCorrelationScope(logger, context, null);
    }

    /// <summary>
    /// Begin a logging scope carrying the ambient correlation id (from
    /// <see cref="OrionContext.Current"/>) and the baggage keys named in
    /// <see cref="CorrelationOptions.LoggedBaggageKeys"/>, or return null when no context is
    /// established on the current flow.
    /// </summary>
    /// <param name="logger">The logger to open the scope on.</param>
    /// <param name="options">The options whose <see cref="CorrelationOptions.LoggedBaggageKeys"/> select the baggage to enrich.</param>
    /// <returns>A disposable that ends the scope, or null when there is no ambient context.</returns>
    public static IDisposable? BeginCorrelationScope(this ILogger logger, CorrelationOptions options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        var context = OrionContext.Current;
        return context is null ? null : BeginCorrelationScope(logger, context, options.LoggedBaggageKeys);
    }

    /// <summary>
    /// Begin a logging scope carrying the given context's correlation id and the selected baggage
    /// keys, without reading the ambient context. Use this from code that already holds a context (a
    /// background job, a message consumer) or when enriching outside an OrionLens scope.
    /// </summary>
    /// <param name="logger">The logger to open the scope on.</param>
    /// <param name="context">The context whose id and baggage to enrich with.</param>
    /// <param name="loggedBaggageKeys">The baggage keys to include, or null for the correlation id alone.</param>
    /// <returns>A disposable that ends the scope, or null when the provider does not support scopes.</returns>
    public static IDisposable? BeginCorrelationScope(
        this ILogger logger,
        CorrelationContext context,
        IEnumerable<string>? loggedBaggageKeys = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(context);

        var state = CorrelationLogScope.FromContext(context, loggedBaggageKeys);
        return logger.BeginScope(state);
    }
}
