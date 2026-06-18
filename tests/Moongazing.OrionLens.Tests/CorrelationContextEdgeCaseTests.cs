namespace Moongazing.OrionLens.Tests;

using System.Collections.Generic;

using Moongazing.OrionLens.Context;

using Xunit;

public sealed class CorrelationContextEdgeCaseTests
{
    [Fact]
    public void Create_with_baggage_copies_the_pairs()
    {
        var seed = new Dictionary<string, string> { ["tenant"] = "acme", ["region"] = "eu" };
        var context = CorrelationContext.Create("id", seed);

        Assert.Equal("acme", context.GetBaggage("tenant"));
        Assert.Equal("eu", context.GetBaggage("region"));
        Assert.Equal(2, context.Baggage.Count);
    }

    [Fact]
    public void Create_with_baggage_snapshots_the_source_dictionary()
    {
        var seed = new Dictionary<string, string> { ["tenant"] = "acme" };
        var context = CorrelationContext.Create("id", seed);

        // Mutating the source after creation must not leak into the frozen snapshot.
        seed["tenant"] = "changed";
        seed["added"] = "later";

        Assert.Equal("acme", context.GetBaggage("tenant"));
        Assert.Null(context.GetBaggage("added"));
    }

    [Fact]
    public void Create_with_empty_baggage_yields_empty_context()
    {
        var context = CorrelationContext.Create("id", new Dictionary<string, string>());
        Assert.Empty(context.Baggage);
    }

    [Fact]
    public void Create_rejects_a_null_id()
    {
        Assert.Throws<ArgumentNullException>(() => CorrelationContext.Create(null!));
    }

    [Fact]
    public void Create_rejects_an_empty_id()
    {
        Assert.Throws<ArgumentException>(() => CorrelationContext.Create(string.Empty));
    }

    [Fact]
    public void Create_with_baggage_rejects_a_null_id()
    {
        Assert.Throws<ArgumentNullException>(
            () => CorrelationContext.Create(null!, new Dictionary<string, string>()));
    }

    [Fact]
    public void Create_with_baggage_rejects_an_empty_id()
    {
        Assert.Throws<ArgumentException>(
            () => CorrelationContext.Create(string.Empty, new Dictionary<string, string>()));
    }

    [Fact]
    public void Create_with_baggage_rejects_a_null_baggage()
    {
        Assert.Throws<ArgumentNullException>(() => CorrelationContext.Create("id", null!));
    }

    [Fact]
    public void Baggage_lookup_is_case_sensitive()
    {
        var context = CorrelationContext.Create("id").WithBaggage("Tenant", "acme");

        Assert.Equal("acme", context.GetBaggage("Tenant"));
        Assert.Null(context.GetBaggage("tenant"));
    }

    [Fact]
    public void WithBaggage_does_not_mutate_the_original_when_adding_a_second_key()
    {
        var first = CorrelationContext.Create("id").WithBaggage("a", "1");
        var second = first.WithBaggage("b", "2");

        Assert.Null(first.GetBaggage("b"));
        Assert.Equal("1", second.GetBaggage("a"));
        Assert.Equal("2", second.GetBaggage("b"));
        Assert.Single(first.Baggage);
        Assert.Equal(2, second.Baggage.Count);
    }

    [Fact]
    public void WithBaggage_preserves_the_correlation_id_across_chaining()
    {
        var context = CorrelationContext.Create("keep-me")
            .WithBaggage("a", "1")
            .WithBaggage("b", "2");

        Assert.Equal("keep-me", context.CorrelationId);
    }

    [Fact]
    public void WithBaggage_accepts_an_empty_value()
    {
        var context = CorrelationContext.Create("id").WithBaggage("k", string.Empty);
        Assert.Equal(string.Empty, context.GetBaggage("k"));
    }

    [Fact]
    public void WithBaggage_rejects_a_null_key()
    {
        var context = CorrelationContext.Create("id");
        Assert.Throws<ArgumentNullException>(() => context.WithBaggage(null!, "v"));
    }

    [Fact]
    public void WithBaggage_rejects_an_empty_key()
    {
        var context = CorrelationContext.Create("id");
        Assert.Throws<ArgumentException>(() => context.WithBaggage(string.Empty, "v"));
    }

    [Fact]
    public void WithBaggage_rejects_a_null_value()
    {
        var context = CorrelationContext.Create("id");
        Assert.Throws<ArgumentNullException>(() => context.WithBaggage("k", null!));
    }

    [Fact]
    public void GetBaggage_rejects_a_null_key()
    {
        var context = CorrelationContext.Create("id");
        Assert.Throws<ArgumentNullException>(() => context.GetBaggage(null!));
    }

    [Fact]
    public void GetBaggage_rejects_an_empty_key()
    {
        var context = CorrelationContext.Create("id");
        Assert.Throws<ArgumentException>(() => context.GetBaggage(string.Empty));
    }

    [Fact]
    public void Baggage_is_a_read_only_view()
    {
        var context = CorrelationContext.Create("id").WithBaggage("k", "v");
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(context.Baggage);
        Assert.True(context.Baggage.ContainsKey("k"));
    }
}
