namespace Moongazing.OrionLens.Tests;

using Moongazing.OrionLens.Context;

using Xunit;

public sealed class OrionContextTests
{
    [Fact]
    public void Current_is_null_before_any_scope()
    {
        Assert.Null(OrionContext.Current);
    }

    [Fact]
    public void BeginScope_sets_current_and_dispose_restores_previous()
    {
        Assert.Null(OrionContext.Current);

        using (OrionContext.BeginScope(CorrelationContext.Create("outer")))
        {
            Assert.Equal("outer", OrionContext.Current!.CorrelationId);

            using (OrionContext.BeginScope(CorrelationContext.Create("inner")))
            {
                Assert.Equal("inner", OrionContext.Current!.CorrelationId);
            }

            Assert.Equal("outer", OrionContext.Current!.CorrelationId);
        }

        Assert.Null(OrionContext.Current);
    }

    [Fact]
    public async Task The_context_flows_across_await()
    {
        using (OrionContext.BeginScope(CorrelationContext.Create("flow")))
        {
            await Task.Yield();
            Assert.Equal("flow", OrionContext.Current!.CorrelationId);
        }
    }
}
