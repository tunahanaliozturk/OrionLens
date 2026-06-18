namespace Moongazing.OrionLens.Tests;

using System.Threading.Tasks;

using Moongazing.OrionLens.Context;

using Xunit;

public sealed class OrionContextEdgeCaseTests
{
    [Fact]
    public void BeginScope_rejects_a_null_context()
    {
        Assert.Throws<ArgumentNullException>(() => OrionContext.BeginScope(null!));
    }

    [Fact]
    public void BeginScope_returns_a_disposable_scope()
    {
        using var scope = OrionContext.BeginScope(CorrelationContext.Create("id"));
        Assert.NotNull(scope);
        Assert.IsAssignableFrom<IDisposable>(scope);
    }

    [Fact]
    public void Current_exposes_the_full_context_including_baggage()
    {
        using (OrionContext.BeginScope(CorrelationContext.Create("id").WithBaggage("tenant", "acme")))
        {
            Assert.Equal("id", OrionContext.Current!.CorrelationId);
            Assert.Equal("acme", OrionContext.Current!.GetBaggage("tenant"));
        }
    }

    [Fact]
    public void Disposing_a_scope_twice_is_a_safe_no_op()
    {
        var scope = OrionContext.BeginScope(CorrelationContext.Create("once"));
        Assert.Equal("once", OrionContext.Current!.CorrelationId);

        scope.Dispose();
        Assert.Null(OrionContext.Current);

        // A second dispose must not resurrect the prior value or throw.
        scope.Dispose();
        Assert.Null(OrionContext.Current);
    }

    [Fact]
    public void Scopes_disposed_out_of_order_restore_the_value_each_scope_captured_at_begin()
    {
        // A guard scope wraps the whole test so any ambient value left behind by the deliberate
        // out-of-order disposal below is restored to null when the guard is disposed, keeping the
        // ambient flow clean for any sibling test scheduled on this execution context.
        using (OrionContext.BeginScope(CorrelationContext.Create("guard")))
        {
            var outer = OrionContext.BeginScope(CorrelationContext.Create("outer"));
            var inner = OrionContext.BeginScope(CorrelationContext.Create("inner"));

            Assert.Equal("inner", OrionContext.Current!.CorrelationId);

            // Each scope blindly restores the value it captured at BeginScope, with no awareness
            // of what is current now. Disposing out of order therefore corrupts the ambient value:
            //   outer captured previous = "guard" -> disposing outer sets Current to "guard"
            //   inner captured previous = "outer" -> disposing inner sets Current back to "outer"
            // This documents the LIFO contract: scopes MUST be disposed in reverse order.
            outer.Dispose();
            Assert.Equal("guard", OrionContext.Current!.CorrelationId);

            inner.Dispose();
            Assert.Equal("outer", OrionContext.Current!.CorrelationId);
        }

        Assert.Null(OrionContext.Current);
    }

    [Fact]
    public void Three_levels_of_nesting_restore_each_layer_on_dispose()
    {
        using (OrionContext.BeginScope(CorrelationContext.Create("a")))
        {
            using (OrionContext.BeginScope(CorrelationContext.Create("b")))
            {
                using (OrionContext.BeginScope(CorrelationContext.Create("c")))
                {
                    Assert.Equal("c", OrionContext.Current!.CorrelationId);
                }

                Assert.Equal("b", OrionContext.Current!.CorrelationId);
            }

            Assert.Equal("a", OrionContext.Current!.CorrelationId);
        }

        Assert.Null(OrionContext.Current);
    }

    [Fact]
    public async Task A_mutation_on_a_child_task_does_not_leak_back_to_the_parent()
    {
        using (OrionContext.BeginScope(CorrelationContext.Create("parent")))
        {
            await Task.Run(() =>
            {
                // AsyncLocal copy-on-write: a scope opened on the child flow is invisible to
                // the parent once the child completes.
                using (OrionContext.BeginScope(CorrelationContext.Create("child")))
                {
                    Assert.Equal("child", OrionContext.Current!.CorrelationId);
                }
            });

            Assert.Equal("parent", OrionContext.Current!.CorrelationId);
        }
    }

    [Fact]
    public async Task The_context_flows_across_a_real_delay()
    {
        using (OrionContext.BeginScope(CorrelationContext.Create("flow")))
        {
            await Task.Delay(1);
            Assert.Equal("flow", OrionContext.Current!.CorrelationId);
        }
    }

    [Fact]
    public async Task Parallel_flows_each_keep_their_own_ambient_context()
    {
        async Task<string?> RunWith(string id)
        {
            using (OrionContext.BeginScope(CorrelationContext.Create(id)))
            {
                await Task.Yield();
                await Task.Delay(5);
                return OrionContext.Current?.CorrelationId;
            }
        }

        var results = await Task.WhenAll(RunWith("one"), RunWith("two"), RunWith("three"));

        Assert.Equal("one", results[0]);
        Assert.Equal("two", results[1]);
        Assert.Equal("three", results[2]);
    }

    [Fact]
    public void Re_entering_the_same_id_is_distinguishable_only_by_reference()
    {
        var context = CorrelationContext.Create("same");
        using (OrionContext.BeginScope(context))
        {
            using (OrionContext.BeginScope(context))
            {
                Assert.Same(context, OrionContext.Current);
            }

            Assert.Same(context, OrionContext.Current);
        }

        Assert.Null(OrionContext.Current);
    }
}
