namespace Moongazing.OrionLens.AspNetCore;

using Microsoft.AspNetCore.Http;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;

/// <summary>
/// ASP.NET Core middleware that establishes the ambient correlation context for each request: it
/// reads the inbound correlation id and baggage (minting an id when missing), makes the context
/// current for the rest of the pipeline, and echoes the id back on the response.
/// </summary>
public sealed class CorrelationMiddleware
{
    private readonly RequestDelegate next;
    private readonly CorrelationOptions options;

    /// <summary>Create the middleware.</summary>
    /// <param name="next">The next delegate.</param>
    /// <param name="options">The propagation options.</param>
    public CorrelationMiddleware(RequestDelegate next, CorrelationOptions options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);
        this.next = next;
        this.options = options;
    }

    /// <summary>Process a request.</summary>
    /// <param name="context">The request context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlation = CorrelationPropagator.Extract(
            header => context.Request.Headers.TryGetValue(header, out var value) ? value.ToString() : null,
            options);

        if (options.WriteResponseHeader)
        {
            // Set before the response starts (this middleware runs early, before any body write),
            // so the caller always sees the id even on an error response.
            context.Response.Headers[options.CorrelationHeader] = correlation.CorrelationId;
        }

        using (OrionContext.BeginScope(correlation))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}
