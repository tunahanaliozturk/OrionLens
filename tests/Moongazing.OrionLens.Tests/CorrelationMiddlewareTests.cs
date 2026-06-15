namespace Moongazing.OrionLens.Tests;

using Microsoft.AspNetCore.Http;

using Moongazing.OrionLens;
using Moongazing.OrionLens.AspNetCore;
using Moongazing.OrionLens.Context;

using Xunit;

public sealed class CorrelationMiddlewareTests
{
    [Fact]
    public async Task It_makes_the_inbound_id_the_ambient_context()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "inbound-id";

        string? observed = null;
        var middleware = new CorrelationMiddleware(_ =>
        {
            observed = OrionContext.Current?.CorrelationId;
            return Task.CompletedTask;
        }, new CorrelationOptions());

        await middleware.InvokeAsync(context);

        Assert.Equal("inbound-id", observed);
        Assert.Equal("inbound-id", context.Response.Headers["X-Correlation-ID"].ToString());
    }

    [Fact]
    public async Task It_generates_an_id_when_the_request_has_none()
    {
        var context = new DefaultHttpContext();

        string? observed = null;
        var middleware = new CorrelationMiddleware(_ =>
        {
            observed = OrionContext.Current?.CorrelationId;
            return Task.CompletedTask;
        }, new CorrelationOptions());

        await middleware.InvokeAsync(context);

        Assert.False(string.IsNullOrEmpty(observed));
        Assert.Equal(observed, context.Response.Headers["X-Correlation-ID"].ToString());
    }

    [Fact]
    public async Task The_ambient_context_is_cleared_after_the_request()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationMiddleware(_ => Task.CompletedTask, new CorrelationOptions());

        await middleware.InvokeAsync(context);

        Assert.Null(OrionContext.Current);
    }
}
