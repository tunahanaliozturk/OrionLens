namespace Moongazing.OrionLens.Tests;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;
using Moongazing.OrionLens.Http;

using Xunit;

public sealed class CorrelationPropagationHandlerTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    private static HttpClient Client(CapturingHandler inner) =>
        new(new CorrelationPropagationHandler(new CorrelationOptions()) { InnerHandler = inner });

    [Fact]
    public async Task It_injects_the_ambient_context_into_the_outbound_request()
    {
        var inner = new CapturingHandler();
        using var client = Client(inner);

        using (OrionContext.BeginScope(CorrelationContext.Create("ctx-id").WithBaggage("tenant", "acme")))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
            await client.SendAsync(request);
        }

        Assert.True(inner.LastRequest!.Headers.TryGetValues("X-Correlation-ID", out var id));
        Assert.Equal("ctx-id", id!.Single());
        Assert.True(inner.LastRequest.Headers.TryGetValues("X-Orion-Baggage", out var baggage));
        Assert.Contains("tenant", baggage!.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task It_sends_no_correlation_header_when_there_is_no_ambient_context()
    {
        var inner = new CapturingHandler();
        using var client = Client(inner);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
        await client.SendAsync(request);

        Assert.False(inner.LastRequest!.Headers.Contains("X-Correlation-ID"));
    }
}
