namespace Moongazing.OrionLens.Tests;

using System.Collections.Generic;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;

using Xunit;

public sealed class CorrelationPropagatorEdgeCaseTests
{
    private static readonly CorrelationOptions Options = new();

    [Fact]
    public void Extract_rejects_a_null_getter()
    {
        Assert.Throws<ArgumentNullException>(() => CorrelationPropagator.Extract(null!, Options));
    }

    [Fact]
    public void Extract_rejects_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => CorrelationPropagator.Extract(_ => null, null!));
    }

    [Fact]
    public void Extract_treats_an_empty_id_header_as_missing_and_generates_one()
    {
        var headers = new Dictionary<string, string> { ["X-Correlation-ID"] = string.Empty };
        var context = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.False(string.IsNullOrEmpty(context.CorrelationId));
        Assert.NotEqual("unknown", context.CorrelationId);
    }

    [Fact]
    public void Extract_generates_a_thirty_two_character_hex_id()
    {
        var context = CorrelationPropagator.Extract(_ => null, Options);

        // Guid "N" format: 32 lowercase hex digits, no dashes.
        Assert.Equal(32, context.CorrelationId.Length);
        Assert.All(context.CorrelationId, c => Assert.True(Uri.IsHexDigit(c)));
    }

    [Fact]
    public void Extract_generates_distinct_ids_on_each_call()
    {
        var first = CorrelationPropagator.Extract(_ => null, Options).CorrelationId;
        var second = CorrelationPropagator.Extract(_ => null, Options).CorrelationId;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Extract_uses_a_sentinel_id_for_an_empty_header_when_generation_is_off()
    {
        // Documents real behavior: although the XML doc says the id is "taken verbatim (which may
        // be empty)" when generation is off, the implementation substitutes the literal "unknown"
        // because the inbound header is empty. See suspected-bug note in the report.
        var options = new CorrelationOptions { GenerateIdWhenMissing = false };
        var context = CorrelationPropagator.Extract(_ => null, options);

        Assert.Equal("unknown", context.CorrelationId);
    }

    [Fact]
    public void Extract_keeps_a_non_empty_inbound_id_even_when_generation_is_off()
    {
        var options = new CorrelationOptions { GenerateIdWhenMissing = false };
        var headers = new Dictionary<string, string> { ["X-Correlation-ID"] = "supplied" };
        var context = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), options);

        Assert.Equal("supplied", context.CorrelationId);
    }

    [Fact]
    public void Extract_ignores_a_missing_baggage_header()
    {
        var headers = new Dictionary<string, string> { ["X-Correlation-ID"] = "id" };
        var context = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.Empty(context.Baggage);
    }

    [Fact]
    public void Extract_ignores_an_empty_baggage_header()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Correlation-ID"] = "id",
            ["X-Orion-Baggage"] = string.Empty,
        };
        var context = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.Empty(context.Baggage);
    }

    [Fact]
    public void Extract_parses_multiple_baggage_pairs()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Correlation-ID"] = "id",
            ["X-Orion-Baggage"] = "tenant=acme,region=eu",
        };
        var context = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.Equal("acme", context.GetBaggage("tenant"));
        Assert.Equal("eu", context.GetBaggage("region"));
    }

    [Fact]
    public void Extract_trims_whitespace_around_baggage_pairs()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Correlation-ID"] = "id",
            ["X-Orion-Baggage"] = " tenant=acme ,  region=eu ",
        };
        var context = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.Equal("acme", context.GetBaggage("tenant"));
        Assert.Equal("eu", context.GetBaggage("region"));
    }

    [Fact]
    public void Extract_skips_malformed_baggage_entries_without_an_equals_sign()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Correlation-ID"] = "id",
            ["X-Orion-Baggage"] = "novalue,tenant=acme",
        };
        var context = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.Null(context.GetBaggage("novalue"));
        Assert.Equal("acme", context.GetBaggage("tenant"));
        Assert.Single(context.Baggage);
    }

    [Fact]
    public void Extract_skips_a_baggage_entry_with_an_empty_key()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Correlation-ID"] = "id",
            ["X-Orion-Baggage"] = "=orphan,tenant=acme",
        };
        var context = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.Equal("acme", context.GetBaggage("tenant"));
        Assert.Single(context.Baggage);
    }

    [Fact]
    public void Extract_keeps_a_baggage_value_that_contains_equals_signs()
    {
        // Only the first '=' separates key from value, so a value may itself contain '='.
        var headers = new Dictionary<string, string>
        {
            ["X-Correlation-ID"] = "id",
            ["X-Orion-Baggage"] = "token=a=b=c",
        };
        var context = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.Equal("a=b=c", context.GetBaggage("token"));
    }

    [Fact]
    public void Extract_accepts_an_empty_baggage_value()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Correlation-ID"] = "id",
            ["X-Orion-Baggage"] = "flag=",
        };
        var context = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.Equal(string.Empty, context.GetBaggage("flag"));
    }

    [Fact]
    public void Extract_decodes_percent_encoded_keys_and_values()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Correlation-ID"] = "id",
            ["X-Orion-Baggage"] = "a%20b=c%2Cd",
        };
        var context = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.Equal("c,d", context.GetBaggage("a b"));
    }

    [Fact]
    public void Inject_rejects_a_null_context()
    {
        Assert.Throws<ArgumentNullException>(
            () => CorrelationPropagator.Inject(null!, (_, _) => { }, Options));
    }

    [Fact]
    public void Inject_rejects_a_null_setter()
    {
        Assert.Throws<ArgumentNullException>(
            () => CorrelationPropagator.Inject(CorrelationContext.Create("id"), null!, Options));
    }

    [Fact]
    public void Inject_rejects_null_options()
    {
        Assert.Throws<ArgumentNullException>(
            () => CorrelationPropagator.Inject(CorrelationContext.Create("id"), (_, _) => { }, null!));
    }

    [Fact]
    public void Inject_writes_the_correlation_id_header()
    {
        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(CorrelationContext.Create("the-id"), (k, v) => headers[k] = v, Options);

        Assert.Equal("the-id", headers["X-Correlation-ID"]);
    }

    [Fact]
    public void Inject_and_extract_honour_custom_header_names()
    {
        var options = new CorrelationOptions
        {
            CorrelationHeader = "X-Trace-Id",
            BaggageHeader = "X-Trace-Baggage",
        };
        var source = CorrelationContext.Create("trace-1").WithBaggage("tenant", "acme");

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(source, (k, v) => headers[k] = v, options);

        Assert.True(headers.ContainsKey("X-Trace-Id"));
        Assert.True(headers.ContainsKey("X-Trace-Baggage"));

        var roundTripped = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), options);
        Assert.Equal("trace-1", roundTripped.CorrelationId);
        Assert.Equal("acme", roundTripped.GetBaggage("tenant"));
    }

    [Fact]
    public void Round_trip_preserves_unicode_baggage()
    {
        var source = CorrelationContext.Create("id").WithBaggage("city", "Istanbul-Sehir");

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(source, (k, v) => headers[k] = v, Options);
        var roundTripped = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.Equal("Istanbul-Sehir", roundTripped.GetBaggage("city"));
    }

    [Fact]
    public void Round_trip_preserves_a_key_containing_an_equals_sign()
    {
        // The key is percent-encoded on inject, so an '=' inside a key survives the round trip
        // rather than being misread as the key/value separator.
        var source = CorrelationContext.Create("id").WithBaggage("a=b", "v");

        var headers = new Dictionary<string, string>();
        CorrelationPropagator.Inject(source, (k, v) => headers[k] = v, Options);
        var roundTripped = CorrelationPropagator.Extract(h => headers.GetValueOrDefault(h), Options);

        Assert.Equal("v", roundTripped.GetBaggage("a=b"));
    }
}
