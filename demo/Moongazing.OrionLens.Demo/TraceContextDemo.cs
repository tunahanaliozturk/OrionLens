namespace Moongazing.OrionLens.Demo;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;

/// <summary>
/// Shows the W3C trace-context bridge enabled by <see cref="CorrelationOptions.UseTraceContext"/>.
/// On inject a <c>traceparent</c> is emitted whose trace-id is derived from the correlation id, so a
/// downstream service that reads the trace sees the same id as <c>X-Correlation-ID</c>. On extract,
/// when no correlation id header is present, the inbound <c>traceparent</c> trace-id becomes the
/// correlation id, so an id minted upstream by a tracing system flows in unchanged.
/// </summary>
internal static class TraceContextDemo
{
    public static void Run()
    {
        DemoConsole.Header("6. UseTraceContext bridges the correlation id and the W3C traceparent");

        var options = new CorrelationOptions { UseTraceContext = true };

        // Inject side: write an established context. With UseTraceContext on, a traceparent is added
        // whose trace-id is derived from the correlation id (here a Guid "N" string is already 32 hex
        // characters, so it is used as the trace-id verbatim).
        var outbound = CorrelationContext.Create(Guid.NewGuid().ToString("N"));
        var wireHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using (OrionContext.BeginScope(outbound))
        {
            CorrelationPropagator.Inject(
                OrionContext.Current!,
                (name, value) => wireHeaders[name] = value,
                options);
        }

        DemoConsole.Line("Inject produced both the id header and an aligned traceparent:");
        foreach (var (name, value) in wireHeaders)
        {
            DemoConsole.Bullet($"{name}: {value}");
        }

        // Extract side: a request that carries only a traceparent (no X-Correlation-ID). The 32-hex
        // trace-id is adopted as the correlation id.
        var inbound = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
        };

        var rebuilt = CorrelationPropagator.Extract(
            name => inbound.TryGetValue(name, out var v) ? v : null,
            options);

        DemoConsole.Line("Extract adopted the inbound trace-id as the correlation id:");
        DemoConsole.Bullet($"traceparent in   : {inbound["traceparent"]}");
        DemoConsole.Bullet($"correlation id   : {rebuilt.CorrelationId}");

        var aligned = rebuilt.CorrelationId == "4bf92f3577b34da6a3ce929d0e0e4736";
        DemoConsole.Line($"Correlation id matches the inbound trace-id: {aligned}");
    }
}
