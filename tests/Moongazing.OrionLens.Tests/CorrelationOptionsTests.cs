namespace Moongazing.OrionLens.Tests;

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
}
