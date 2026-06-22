namespace Moongazing.OrionLens.Tests;

using Microsoft.Extensions.DependencyInjection;

using Moongazing.OrionLens;

using Xunit;

public sealed class CorrelationOptionsTests
{
    [Fact]
    public void Defaults_match_the_documented_header_names_and_policy()
    {
        var options = new CorrelationOptions();

        Assert.Equal("X-Correlation-ID", options.CorrelationHeader);
        Assert.Equal("X-Orion-Baggage", options.BaggageHeader);
        Assert.True(options.GenerateIdWhenMissing);
        Assert.True(options.WriteResponseHeader);
    }

    [Fact]
    public void Properties_are_settable()
    {
        var options = new CorrelationOptions
        {
            CorrelationHeader = "X-A",
            BaggageHeader = "X-B",
            GenerateIdWhenMissing = false,
            WriteResponseHeader = false,
        };

        Assert.Equal("X-A", options.CorrelationHeader);
        Assert.Equal("X-B", options.BaggageHeader);
        Assert.False(options.GenerateIdWhenMissing);
        Assert.False(options.WriteResponseHeader);
    }

    [Fact]
    public void Baggage_policy_defaults_are_unset()
    {
        var options = new CorrelationOptions();

        Assert.Null(options.MaxBaggageCount);
        Assert.Null(options.MaxBaggageBytes);
        Assert.Empty(options.NonPropagatingBaggageKeys);
        Assert.Empty(options.LoggedBaggageKeys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_rejects_a_non_positive_max_baggage_count(int value)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddOrionLens(o => o.MaxBaggageCount = value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_rejects_a_non_positive_max_baggage_bytes(int value)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddOrionLens(o => o.MaxBaggageBytes = value));
    }

    [Fact]
    public void Validate_accepts_a_positive_baggage_policy()
    {
        var services = new ServiceCollection();

        var exception = Record.Exception(() =>
            services.AddOrionLens(o =>
            {
                o.MaxBaggageCount = 8;
                o.MaxBaggageBytes = 1024;
                o.NonPropagatingBaggageKeys.Add("internal");
                o.LoggedBaggageKeys.Add("tenant");
            }));

        Assert.Null(exception);
    }
}
