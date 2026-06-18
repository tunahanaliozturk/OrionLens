namespace Moongazing.OrionLens.Demo;

using Moongazing.OrionLens.Context;

/// <summary>
/// Shows the immutable, copy-on-write baggage model: <see cref="CorrelationContext.WithBaggage"/>
/// returns a new instance, and opening a nested scope over it lets the inner flow see the added
/// baggage while the outer (parent) context is left untouched and restored on dispose.
/// </summary>
internal static class BaggageCopyOnWriteDemo
{
    public static void Run()
    {
        DemoConsole.Header("3. Baggage is copy-on-write across nested scopes");

        var parent = CorrelationContext.Create("req-9a01bc77").WithBaggage("tenant", "acme");

        using (OrionContext.BeginScope(parent))
        {
            DemoConsole.Line($"Parent scope baggage keys      : [{string.Join(", ", OrionContext.Current!.Baggage.Keys)}]");

            var child = OrionContext.Current!.WithBaggage("region", "eu-west");
            DemoConsole.Bullet($"WithBaggage produced a new instance: {!ReferenceEquals(parent, child)}");
            DemoConsole.Bullet($"Parent instance still has 1 key    : {parent.Baggage.Count == 1}");

            using (OrionContext.BeginScope(child))
            {
                DemoConsole.Line($"Child scope baggage keys       : [{string.Join(", ", OrionContext.Current!.Baggage.Keys)}]");
                DemoConsole.Line($"Child sees region              : {OrionContext.Current!.GetBaggage("region")}");
            }

            DemoConsole.Line($"Back in parent, baggage keys   : [{string.Join(", ", OrionContext.Current!.Baggage.Keys)}] (region gone)");
        }
    }
}
