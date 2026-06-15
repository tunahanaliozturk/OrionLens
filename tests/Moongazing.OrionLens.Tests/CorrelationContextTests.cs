namespace Moongazing.OrionLens.Tests;

using Moongazing.OrionLens.Context;

using Xunit;

public sealed class CorrelationContextTests
{
    [Fact]
    public void Create_sets_the_id_and_empty_baggage()
    {
        var context = CorrelationContext.Create("abc");
        Assert.Equal("abc", context.CorrelationId);
        Assert.Empty(context.Baggage);
    }

    [Fact]
    public void WithBaggage_returns_a_new_context_and_leaves_the_original_untouched()
    {
        var original = CorrelationContext.Create("abc");
        var enriched = original.WithBaggage("tenant", "acme");

        Assert.Empty(original.Baggage);
        Assert.Equal("acme", enriched.GetBaggage("tenant"));
        Assert.Equal("abc", enriched.CorrelationId);
    }

    [Fact]
    public void WithBaggage_replaces_an_existing_key()
    {
        var context = CorrelationContext.Create("abc")
            .WithBaggage("k", "v1")
            .WithBaggage("k", "v2");

        Assert.Equal("v2", context.GetBaggage("k"));
    }

    [Fact]
    public void GetBaggage_returns_null_for_an_absent_key()
    {
        Assert.Null(CorrelationContext.Create("abc").GetBaggage("missing"));
    }
}
