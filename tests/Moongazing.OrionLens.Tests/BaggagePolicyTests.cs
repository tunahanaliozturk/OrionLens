namespace Moongazing.OrionLens.Tests;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;

using Xunit;

public sealed class BaggagePolicyTests
{
    [Fact]
    public void Non_propagating_key_is_not_emitted_on_inject()
    {
        var options = new CorrelationOptions();
        options.NonPropagatingBaggageKeys.Add("internal");

        var context = CorrelationContext.Create("id-1")
            .WithBaggage("tenant", "acme")
            .WithBaggage("internal", "secret");

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(context, (k, v) => headers[k] = v, options);

        var baggage = headers[options.BaggageHeader];
        Assert.Contains("tenant", baggage, StringComparison.Ordinal);
        // The internal key and its value must not cross the boundary.
        Assert.DoesNotContain("internal", baggage, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", baggage, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_propagating_key_is_still_accepted_on_extract()
    {
        // Inbound-only semantics: the mark suppresses outbound emission, it does not reject inbound.
        var options = new CorrelationOptions();
        options.NonPropagatingBaggageKeys.Add("internal");

        var inbound = new Dictionary<string, string>
        {
            [options.CorrelationHeader] = "id-1",
            [options.BaggageHeader] = "internal=secret,tenant=acme",
        };

        var context = CorrelationPropagator.Extract(h => inbound.GetValueOrDefault(h), options);

        Assert.Equal("secret", context.GetBaggage("internal"));
        Assert.Equal("acme", context.GetBaggage("tenant"));
    }

    [Fact]
    public void Inject_with_only_non_propagating_baggage_omits_the_header_entirely()
    {
        var options = new CorrelationOptions();
        options.NonPropagatingBaggageKeys.Add("internal");

        var context = CorrelationContext.Create("id-1").WithBaggage("internal", "secret");

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(context, (k, v) => headers[k] = v, options);

        Assert.True(headers.ContainsKey(options.CorrelationHeader));
        Assert.False(headers.ContainsKey(options.BaggageHeader));
    }

    [Fact]
    public void MaxBaggageCount_caps_the_emitted_pairs_in_ordinal_key_order()
    {
        var options = new CorrelationOptions { MaxBaggageCount = 2 };

        var context = CorrelationContext.Create("id-1")
            .WithBaggage("c", "3")
            .WithBaggage("a", "1")
            .WithBaggage("b", "2");

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(context, (k, v) => headers[k] = v, options);

        // The two lowest ordinal keys (a, b) survive; c is dropped.
        var roundTripped = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), new CorrelationOptions());
        Assert.Equal("1", roundTripped.GetBaggage("a"));
        Assert.Equal("2", roundTripped.GetBaggage("b"));
        Assert.Null(roundTripped.GetBaggage("c"));
    }

    [Fact]
    public void MaxBaggageCount_caps_inbound_pairs_on_extract()
    {
        var options = new CorrelationOptions { MaxBaggageCount = 1 };

        var inbound = new Dictionary<string, string>
        {
            [options.CorrelationHeader] = "id-1",
            [options.BaggageHeader] = "a=1,b=2,c=3",
        };

        var context = CorrelationPropagator.Extract(h => inbound.GetValueOrDefault(h), options);

        // Only the first inbound pair is admitted; the rest are dropped to bound memory.
        Assert.Equal("1", context.GetBaggage("a"));
        Assert.Null(context.GetBaggage("b"));
        Assert.Null(context.GetBaggage("c"));
    }

    [Fact]
    public void MaxBaggageBytes_drops_pairs_that_would_exceed_the_encoded_limit_on_inject()
    {
        // "a=1" is 3 bytes; "a=1,bb=22" is 9. A 4-byte budget admits only the first pair.
        var options = new CorrelationOptions { MaxBaggageBytes = 4 };

        var context = CorrelationContext.Create("id-1")
            .WithBaggage("a", "1")
            .WithBaggage("bb", "22");

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(context, (k, v) => headers[k] = v, options);

        var baggage = headers[options.BaggageHeader];
        Assert.Equal("a=1", baggage);
    }

    [Fact]
    public void MaxBaggageBytes_caps_inbound_value_on_extract()
    {
        var options = new CorrelationOptions { MaxBaggageBytes = 4 };

        var inbound = new Dictionary<string, string>
        {
            [options.CorrelationHeader] = "id-1",
            [options.BaggageHeader] = "a=1,b=2,c=3",
        };

        var context = CorrelationPropagator.Extract(h => inbound.GetValueOrDefault(h), options);

        // "a=1" (3 bytes) fits; adding ",b=2" would reach 7 bytes, past the 4-byte budget.
        Assert.Equal("1", context.GetBaggage("a"));
        Assert.Null(context.GetBaggage("b"));
        Assert.Null(context.GetBaggage("c"));
    }

    [Fact]
    public void No_policy_leaves_baggage_propagation_unchanged()
    {
        // Behaviour-compatibility guard: with no policy configured, every pair round-trips.
        var options = new CorrelationOptions();

        var context = CorrelationContext.Create("id-1")
            .WithBaggage("a", "1")
            .WithBaggage("b", "2")
            .WithBaggage("c", "3");

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(context, (k, v) => headers[k] = v, options);
        var roundTripped = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), options);

        Assert.Equal("1", roundTripped.GetBaggage("a"));
        Assert.Equal("2", roundTripped.GetBaggage("b"));
        Assert.Equal("3", roundTripped.GetBaggage("c"));
    }

    [Fact]
    public void No_policy_emits_a_single_pair_with_the_unchanged_wire_encoding()
    {
        // The no-policy fast path must keep the exact pre-policy header encoding (percent-encoded
        // key=value), not the sorted/capped policy form.
        var options = new CorrelationOptions();

        var context = CorrelationContext.Create("id-1").WithBaggage("tenant id", "ac me");

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(context, (k, v) => headers[k] = v, options);

        Assert.Equal("tenant%20id=ac%20me", headers[options.BaggageHeader]);
    }

    [Fact]
    public void Count_and_non_propagating_policy_compose_on_inject()
    {
        // The non-propagating key is removed first, then the count cap applies to what remains.
        var options = new CorrelationOptions { MaxBaggageCount = 1 };
        options.NonPropagatingBaggageKeys.Add("a");

        var context = CorrelationContext.Create("id-1")
            .WithBaggage("a", "1")
            .WithBaggage("b", "2")
            .WithBaggage("c", "3");

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(context, (k, v) => headers[k] = v, options);

        // "a" is dropped as non-propagating; of the remaining {b, c}, the count cap keeps "b".
        Assert.Equal("b=2", headers[options.BaggageHeader]);
    }
}
