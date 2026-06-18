namespace Moongazing.OrionLens.Tests;

using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using Moongazing.OrionLens;
using Moongazing.OrionLens.AspNetCore;
using Moongazing.OrionLens.Context;

using Xunit;

public sealed class CorrelationMiddlewareEdgeCaseTests
{
    private static CorrelationMiddleware Middleware(RequestDelegate next, CorrelationOptions? options = null)
        => new(next, options ?? new CorrelationOptions());

    [Fact]
    public void Constructor_rejects_a_null_next()
    {
        Assert.Throws<ArgumentNullException>(() => new CorrelationMiddleware(null!, new CorrelationOptions()));
    }

    [Fact]
    public void Constructor_rejects_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => new CorrelationMiddleware(_ => Task.CompletedTask, null!));
    }

    [Fact]
    public async Task InvokeAsync_rejects_a_null_context()
    {
        var middleware = Middleware(_ => Task.CompletedTask);
        await Assert.ThrowsAsync<ArgumentNullException>(() => middleware.InvokeAsync(null!));
    }

    [Fact]
    public async Task It_makes_inbound_baggage_part_of_the_ambient_context()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "id";
        context.Request.Headers["X-Orion-Baggage"] = "tenant=acme";

        string? tenant = null;
        var middleware = Middleware(_ =>
        {
            tenant = OrionContext.Current?.GetBaggage("tenant");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal("acme", tenant);
    }

    [Fact]
    public async Task It_does_not_write_the_response_header_when_disabled()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "id";

        var middleware = Middleware(
            _ => Task.CompletedTask,
            new CorrelationOptions { WriteResponseHeader = false });

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task It_echoes_a_custom_response_header_name()
    {
        var options = new CorrelationOptions { CorrelationHeader = "X-Trace-Id" };
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Trace-Id"] = "trace-1";

        var middleware = Middleware(_ => Task.CompletedTask, options);
        await middleware.InvokeAsync(context);

        Assert.Equal("trace-1", context.Response.Headers["X-Trace-Id"].ToString());
    }

    [Fact]
    public async Task It_uses_the_sentinel_id_when_generation_is_off_and_no_header_is_sent()
    {
        var options = new CorrelationOptions { GenerateIdWhenMissing = false };
        var context = new DefaultHttpContext();

        string? observed = null;
        var middleware = Middleware(_ =>
        {
            observed = OrionContext.Current?.CorrelationId;
            return Task.CompletedTask;
        }, options);

        await middleware.InvokeAsync(context);

        Assert.Equal("unknown", observed);
        Assert.Equal("unknown", context.Response.Headers["X-Correlation-ID"].ToString());
    }

    [Fact]
    public async Task It_restores_the_ambient_context_even_when_the_pipeline_throws()
    {
        var context = new DefaultHttpContext();
        var middleware = Middleware(_ => throw new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.Null(OrionContext.Current);
    }

    [Fact]
    public async Task The_generated_id_is_stable_within_a_single_request()
    {
        var context = new DefaultHttpContext();

        string? observed = null;
        var middleware = Middleware(_ =>
        {
            observed = OrionContext.Current?.CorrelationId;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        // The id the downstream pipeline saw must be exactly the id echoed on the response.
        Assert.Equal(observed, context.Response.Headers["X-Correlation-ID"].ToString());
    }
}
