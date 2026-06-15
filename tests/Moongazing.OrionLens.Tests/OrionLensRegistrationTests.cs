namespace Moongazing.OrionLens.Tests;

using Microsoft.Extensions.DependencyInjection;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Http;

using Xunit;

public sealed class OrionLensRegistrationTests
{
    [Fact]
    public void AddOrionLens_registers_the_options_and_handler()
    {
        var services = new ServiceCollection();
        services.AddOrionLens();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<CorrelationOptions>());
        Assert.NotNull(provider.GetService<CorrelationPropagationHandler>());
    }

    [Fact]
    public void AddOrionLens_honours_configured_options()
    {
        var services = new ServiceCollection();
        services.AddOrionLens(o => o.CorrelationHeader = "X-Trace");

        using var provider = services.BuildServiceProvider();
        Assert.Equal("X-Trace", provider.GetRequiredService<CorrelationOptions>().CorrelationHeader);
    }

    [Fact]
    public void AddOrionLens_rejects_invalid_options_eagerly()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddOrionLens(o => o.CorrelationHeader = ""));
    }
}
