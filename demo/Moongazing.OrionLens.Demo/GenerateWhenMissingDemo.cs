namespace Moongazing.OrionLens.Demo;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;

/// <summary>
/// Shows the inbound-edge behaviour of <see cref="CorrelationPropagator.Extract"/> when no id header
/// is present: with <see cref="CorrelationOptions.GenerateIdWhenMissing"/> on (the default) a fresh
/// id is minted; with it off the id is taken verbatim (empty by default); and a non-empty
/// placeholder is opt-in via <see cref="CorrelationOptions.MissingIdSentinel"/>.
/// </summary>
internal static class GenerateWhenMissingDemo
{
    public static void Run()
    {
        DemoConsole.Header("5. Extract mints (or takes verbatim) an id when the inbound header is missing");

        var emptyHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Func<string, string?> noHeaders = name => emptyHeaders.TryGetValue(name, out var v) ? v : null;

        var minted = CorrelationPropagator.Extract(noHeaders, new CorrelationOptions { GenerateIdWhenMissing = true });
        DemoConsole.Line($"GenerateIdWhenMissing = true                  -> minted id: {minted.CorrelationId}");

        var verbatim = CorrelationPropagator.Extract(noHeaders, new CorrelationOptions { GenerateIdWhenMissing = false });
        DemoConsole.Line($"GenerateIdWhenMissing = false                 -> verbatim : '{verbatim.CorrelationId}' (empty)");

        var sentinel = CorrelationPropagator.Extract(
            noHeaders,
            new CorrelationOptions { GenerateIdWhenMissing = false, MissingIdSentinel = "unknown" });
        DemoConsole.Line($"GenerateIdWhenMissing = false, sentinel set   -> sentinel : {sentinel.CorrelationId}");
    }
}
