namespace Moongazing.OrionLens.Tests;

using System.Threading.Tasks;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;
using Moongazing.OrionLens.Http;

using Xunit;

public sealed class CorrelationPropagationHandlerEdgeCaseTests
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

    private static HttpClient Client(CapturingHandler inner, CorrelationOptions? options = null) =>
        new(new CorrelationPropagationHandler(options ?? new CorrelationOptions()) { InnerHandler = inner });

    [Fact]
    public void Constructor_rejects_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => new CorrelationPropagationHandler(null!));
    }

    [Fact]
    public async Task It_sends_the_id_but_no_baggage_header_when_baggage_is_empty()
    {
        var inner = new CapturingHandler();
        using var client = Client(inner);

        using (OrionContext.BeginScope(CorrelationContext.Create("id-only")))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
            await client.SendAsync(request);
        }

        Assert.True(inner.LastRequest!.Headers.Contains("X-Correlation-ID"));
        Assert.False(inner.LastRequest.Headers.Contains("X-Orion-Baggage"));
    }

    [Fact]
    public async Task It_overwrites_a_pre_existing_correlation_header_rather_than_appending()
    {
        var inner = new CapturingHandler();
        using var client = Client(inner);

        using (OrionContext.BeginScope(CorrelationContext.Create("ambient-id")))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", "stale-id");

            await client.SendAsync(request);
        }

        Assert.True(inner.LastRequest!.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.Equal("ambient-id", Assert.Single(values!));
    }

    [Fact]
    public async Task It_uses_custom_header_names_from_the_options()
    {
        var inner = new CapturingHandler();
        var options = new CorrelationOptions { CorrelationHeader = "X-Trace-Id" };
        using var client = Client(inner, options);

        using (OrionContext.BeginScope(CorrelationContext.Create("trace-1")))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
            await client.SendAsync(request);
        }

        Assert.True(inner.LastRequest!.Headers.TryGetValues("X-Trace-Id", out var values));
        Assert.Equal("trace-1", Assert.Single(values!));
    }

    [Fact]
    public async Task The_injected_id_can_be_extracted_back_on_the_receiving_side()
    {
        var inner = new CapturingHandler();
        using var client = Client(inner);

        using (OrionContext.BeginScope(CorrelationContext.Create("end-to-end").WithBaggage("tenant", "acme")))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
            await client.SendAsync(request);
        }

        var rebuilt = CorrelationPropagator.Extract(
            header => inner.LastRequest!.Headers.TryGetValues(header, out var v) ? string.Join(",", v) : null,
            new CorrelationOptions());

        Assert.Equal("end-to-end", rebuilt.CorrelationId);
        Assert.Equal("acme", rebuilt.GetBaggage("tenant"));
    }
}
