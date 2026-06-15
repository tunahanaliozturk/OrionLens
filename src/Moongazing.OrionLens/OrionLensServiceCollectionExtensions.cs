namespace Moongazing.OrionLens;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moongazing.OrionLens.AspNetCore;
using Moongazing.OrionLens.Http;

/// <summary>
/// Registration and pipeline helpers for OrionLens.
/// </summary>
public static class OrionLensServiceCollectionExtensions
{
    /// <summary>Register the correlation options and the outbound propagation handler.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional propagation configuration.</param>
    public static IServiceCollection AddOrionLens(
        this IServiceCollection services,
        Action<CorrelationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new CorrelationOptions();
        configure?.Invoke(options);
        options.Validate();

        services.TryAddSingleton(options);
        services.TryAddTransient<CorrelationPropagationHandler>();

        return services;
    }

    /// <summary>
    /// Add the correlation middleware to the pipeline. Place it early, before anything that logs or
    /// makes downstream calls, so the ambient context is established for the whole request.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public static IApplicationBuilder UseOrionLens(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<CorrelationMiddleware>();
    }
}
