namespace Moongazing.OrionLens.Demo;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;

/// <summary>
/// Demonstrates transport-agnostic propagation. The "sender" uses
/// <see cref="CorrelationPropagator.Inject"/> to write the ambient context onto an outbound header
/// bag; the "receiver" uses <see cref="CorrelationPropagator.Extract"/> to rebuild an equivalent
/// context from those same headers, then runs under it. No HTTP host is started: the header bag
/// stands in for any transport (HttpClient, a message envelope, a queue).
/// </summary>
internal static class PropagationDemo
{
    public static void Run()
    {
        DemoConsole.Header("4. Inject outbound headers, then Extract on the receiving side");

        var options = new CorrelationOptions();

        // Sender side: an established context that we want to carry to a downstream service.
        var outbound = CorrelationContext.Create("trace-5e2d11aa")
            .WithBaggage("tenant", "acme")
            .WithBaggage("locale", "it-IT");

        var wireHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using (OrionContext.BeginScope(outbound))
        {
            CorrelationPropagator.Inject(
                OrionContext.Current!,
                (name, value) => wireHeaders[name] = value,
                options);
        }

        DemoConsole.Line("Outbound headers produced by Inject:");
        foreach (var (name, value) in wireHeaders)
        {
            DemoConsole.Bullet($"{name}: {value}");
        }

        // Receiver side: a different logical flow that only has the wire headers to work from.
        var rebuilt = CorrelationPropagator.Extract(
            name => wireHeaders.TryGetValue(name, out var v) ? v : null,
            options);

        using (OrionContext.BeginScope(rebuilt))
        {
            var current = OrionContext.Current!;
            DemoConsole.Line("Receiver rebuilt context from the headers:");
            DemoConsole.Bullet($"correlation id : {current.CorrelationId}");
            DemoConsole.Bullet($"tenant         : {current.GetBaggage("tenant")}");
            DemoConsole.Bullet($"locale         : {current.GetBaggage("locale")}");

            var idMatches = current.CorrelationId == outbound.CorrelationId;
            DemoConsole.Line($"Round-trip preserved the id: {idMatches}");
        }
    }
}
