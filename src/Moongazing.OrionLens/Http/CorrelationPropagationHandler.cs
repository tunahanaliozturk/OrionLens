namespace Moongazing.OrionLens.Http;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;

/// <summary>
/// A <see cref="DelegatingHandler"/> that injects the ambient correlation context into every
/// outbound request, so the id and baggage established at the edge follow calls to downstream
/// services. Add it to a typed or named <see cref="System.Net.Http.HttpClient"/>.
/// </summary>
public sealed class CorrelationPropagationHandler : DelegatingHandler
{
    private readonly CorrelationOptions options;

    /// <summary>Create the handler.</summary>
    /// <param name="options">The propagation options.</param>
    public CorrelationPropagationHandler(CorrelationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = OrionContext.Current;
        if (current is not null)
        {
            CorrelationPropagator.Inject(current, (name, value) =>
            {
                request.Headers.Remove(name);
                request.Headers.TryAddWithoutValidation(name, value);
            }, options);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
