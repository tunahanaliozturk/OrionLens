namespace Moongazing.OrionLens.Tests;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;

using Xunit;

/// <summary>
/// v0.4.0: W3C <c>baggage</c> header interop. With <see cref="CorrelationOptions.UseW3CBaggage"/> set,
/// OrionLens reads and writes the standard <c>baggage</c> header alongside the custom
/// <c>X-Orion-Baggage</c> channel, while honouring the 0.3.0 baggage policy (non-propagating keys stay
/// off the wire on both headers).
/// </summary>
public sealed class W3CBaggageInteropTests
{
    [Fact]
    public void Baggage_is_written_to_the_standard_w3c_header_when_enabled()
    {
        var options = new CorrelationOptions { UseW3CBaggage = true };

        var context = CorrelationContext.Create("id-1").WithBaggage("tenant", "acme");

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(context, (k, v) => headers[k] = v, options);

        // Emitted on both the custom and the standard header.
        Assert.Equal("tenant=acme", headers[options.BaggageHeader]);
        Assert.Equal("tenant=acme", headers[options.W3CBaggageHeader]);
    }

    [Fact]
    public void Standard_w3c_header_is_not_written_when_interop_is_off()
    {
        var options = new CorrelationOptions();

        var context = CorrelationContext.Create("id-1").WithBaggage("tenant", "acme");

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(context, (k, v) => headers[k] = v, options);

        Assert.True(headers.ContainsKey(options.BaggageHeader));
        Assert.False(headers.ContainsKey(options.W3CBaggageHeader));
    }

    [Fact]
    public void Baggage_round_trips_through_the_w3c_header_while_a_non_propagating_key_is_not_emitted()
    {
        // The headline interop guarantee: a propagating key survives a W3C-baggage round-trip, while a
        // non-propagating key is admitted inbound but never re-emitted on either outbound header.
        var inject = new CorrelationOptions { UseW3CBaggage = true };
        inject.NonPropagatingBaggageKeys.Add("internal");

        var context = CorrelationContext.Create("id-1")
            .WithBaggage("tenant", "acme")
            .WithBaggage("internal", "secret");

        // Inject: capture the headers actually placed on the wire.
        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(context, (k, v) => headers[k] = v, inject);

        // The standard baggage header carries tenant but not the non-propagating internal key.
        var w3c = headers[inject.W3CBaggageHeader];
        Assert.Contains("tenant", w3c, StringComparison.Ordinal);
        Assert.DoesNotContain("internal", w3c, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", w3c, StringComparison.Ordinal);

        // Re-extract from the standard header alone (drop the custom header to prove the W3C channel
        // round-trips on its own).
        var inbound = new Dictionary<string, string>
        {
            [inject.CorrelationHeader] = headers[inject.CorrelationHeader],
            [inject.W3CBaggageHeader] = w3c,
        };
        var extract = new CorrelationOptions { UseW3CBaggage = true };
        var roundTripped = CorrelationPropagator.Extract(h => inbound.GetValueOrDefault(h), extract);

        Assert.Equal("acme", roundTripped.GetBaggage("tenant"));
        Assert.Null(roundTripped.GetBaggage("internal"));
    }

    [Fact]
    public void Extract_reads_the_standard_w3c_header_when_the_custom_header_is_absent()
    {
        var options = new CorrelationOptions { UseW3CBaggage = true };

        var inbound = new Dictionary<string, string>
        {
            [options.CorrelationHeader] = "id-1",
            [options.W3CBaggageHeader] = "region=eu,tenant=acme",
        };

        var context = CorrelationPropagator.Extract(h => inbound.GetValueOrDefault(h), options);

        Assert.Equal("eu", context.GetBaggage("region"));
        Assert.Equal("acme", context.GetBaggage("tenant"));
    }

    [Fact]
    public void Custom_header_wins_over_the_w3c_header_on_a_key_collision()
    {
        var options = new CorrelationOptions { UseW3CBaggage = true };

        var inbound = new Dictionary<string, string>
        {
            [options.CorrelationHeader] = "id-1",
            [options.BaggageHeader] = "tenant=fromcustom",
            [options.W3CBaggageHeader] = "tenant=fromw3c",
        };

        var context = CorrelationPropagator.Extract(h => inbound.GetValueOrDefault(h), options);

        // The OrionLens-native channel is authoritative when both carry the same key.
        Assert.Equal("fromcustom", context.GetBaggage("tenant"));
    }

    [Fact]
    public void W3c_header_is_ignored_on_extract_when_interop_is_off()
    {
        var options = new CorrelationOptions();

        var inbound = new Dictionary<string, string>
        {
            [options.CorrelationHeader] = "id-1",
            [options.W3CBaggageHeader] = "tenant=acme",
        };

        var context = CorrelationPropagator.Extract(h => inbound.GetValueOrDefault(h), options);

        Assert.Null(context.GetBaggage("tenant"));
    }
}
