// NativeAOT smoke test. Publishing this with PublishAot=true must produce zero trim/AOT warnings,
// and running it must exit 0 - OrionLens's AOT exit criterion. Runtime checks, not a framework:
// the point is to prove DI registration and the ambient AsyncLocal correlation context survive
// trimming in a real native binary.
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;

var services = new ServiceCollection();
services.AddOrionLens();
using var provider = services.BuildServiceProvider();
_ = provider.GetRequiredService<CorrelationOptions>();

Check(OrionContext.Current is null, "ambient context should start empty");

var context = CorrelationContext.Create("corr-1").WithBaggage("tenant", "acme");
using (OrionContext.BeginScope(context))
{
    Check(OrionContext.Current?.CorrelationId == "corr-1", "correlation id not ambient inside scope");
    Check(OrionContext.Current?.GetBaggage("tenant") == "acme", "baggage not carried in scope");
}

Check(OrionContext.Current is null, "ambient context should be restored after the scope disposes");

Console.WriteLine("OrionLens AOT smoke test passed.");
return 0;

static void Check(bool condition, string message)
{
    if (!condition)
    {
        Console.Error.WriteLine($"AOT smoke test failed: {message}");
        Environment.Exit(1);
    }
}
