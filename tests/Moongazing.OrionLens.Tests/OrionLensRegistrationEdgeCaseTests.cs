namespace Moongazing.OrionLens.Tests;

using Microsoft.Extensions.DependencyInjection;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Http;

using Xunit;

public sealed class OrionLensRegistrationEdgeCaseTests
{
    [Fact]
    public void AddOrionLens_rejects_a_null_service_collection()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddOrionLens());
    }

    [Fact]
    public void AddOrionLens_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();
        var returned = services.AddOrionLens();

        Assert.Same(services, returned);
    }

    [Fact]
    public void AddOrionLens_registers_the_options_as_a_singleton()
    {
        var services = new ServiceCollection();
        services.AddOrionLens();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<CorrelationOptions>();
        var second = provider.GetRequiredService<CorrelationOptions>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddOrionLens_registers_the_handler_as_transient()
    {
        var services = new ServiceCollection();
        services.AddOrionLens();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<CorrelationPropagationHandler>();
        var second = provider.GetRequiredService<CorrelationPropagationHandler>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void AddOrionLens_does_not_overwrite_a_pre_registered_options_instance()
    {
        var preexisting = new CorrelationOptions { CorrelationHeader = "X-Pre" };
        var services = new ServiceCollection();
        services.AddSingleton(preexisting);

        // TryAddSingleton must respect the existing registration; the configure callback is then
        // applied to a throwaway instance that never reaches the container.
        services.AddOrionLens(o => o.CorrelationHeader = "X-Ignored");

        using var provider = services.BuildServiceProvider();
        Assert.Same(preexisting, provider.GetRequiredService<CorrelationOptions>());
        Assert.Equal("X-Pre", provider.GetRequiredService<CorrelationOptions>().CorrelationHeader);
    }

    [Fact]
    public void AddOrionLens_rejects_an_empty_baggage_header()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddOrionLens(o => o.BaggageHeader = ""));
    }

    [Fact]
    public void AddOrionLens_invoked_twice_keeps_the_first_options()
    {
        var services = new ServiceCollection();
        services.AddOrionLens(o => o.CorrelationHeader = "X-First");
        services.AddOrionLens(o => o.CorrelationHeader = "X-Second");

        using var provider = services.BuildServiceProvider();
        Assert.Equal("X-First", provider.GetRequiredService<CorrelationOptions>().CorrelationHeader);
    }

    [Fact]
    public void AddOrionLens_with_no_configure_applies_the_defaults()
    {
        var services = new ServiceCollection();
        services.AddOrionLens();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<CorrelationOptions>();

        Assert.Equal("X-Correlation-ID", options.CorrelationHeader);
        Assert.Equal("X-Orion-Baggage", options.BaggageHeader);
    }
}
