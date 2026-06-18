namespace Moongazing.OrionLens.Demo;

using Moongazing.OrionLens.Context;

/// <summary>
/// Establishes a correlation scope with an explicit id plus baggage and reads it back through
/// <see cref="OrionContext.Current"/> with no parameter passing. Shows that the scope restores the
/// previous (here: empty) context on dispose.
/// </summary>
internal static class AmbientScopeDemo
{
    public static void Run()
    {
        DemoConsole.Header("1. Begin a correlation scope and read OrionContext.Current");

        DemoConsole.Line($"Before any scope, OrionContext.Current is: {(OrionContext.Current is null ? "<null>" : OrionContext.Current.CorrelationId)}");

        var context = CorrelationContext.Create("order-7c3f9a2b")
            .WithBaggage("tenant", "acme")
            .WithBaggage("feature", "fast-checkout");

        using (OrionContext.BeginScope(context))
        {
            var current = OrionContext.Current!;
            DemoConsole.Line($"Inside scope, correlation id : {current.CorrelationId}");
            DemoConsole.Line($"Inside scope, baggage tenant : {current.GetBaggage("tenant")}");
            DemoConsole.Line($"Inside scope, baggage feature: {current.GetBaggage("feature")}");
            DemoConsole.Line($"Absent baggage key returns    : {(current.GetBaggage("missing") is null ? "<null>" : current.GetBaggage("missing"))}");
        }

        DemoConsole.Line($"After dispose, OrionContext.Current is: {(OrionContext.Current is null ? "<null> (previous context restored)" : OrionContext.Current.CorrelationId)}");
    }
}
