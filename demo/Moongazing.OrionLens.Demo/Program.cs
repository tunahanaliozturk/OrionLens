namespace Moongazing.OrionLens.Demo;

/// <summary>
/// Runnable tour of the OrionLens core ambient-context and propagation API. Runs to completion and
/// exits; no web host is started. The ASP.NET Core framework reference is present only so the core
/// library (which uses the shared framework) resolves.
/// </summary>
internal static class Program
{
    private static async Task Main()
    {
        Console.WriteLine("OrionLens demo - ambient correlation context for .NET");

        AmbientScopeDemo.Run();
        await AsyncFlowDemo.RunAsync();
        BaggageCopyOnWriteDemo.Run();
        PropagationDemo.Run();
        GenerateWhenMissingDemo.Run();
        TraceContextDemo.Run();

        Console.WriteLine();
        Console.WriteLine("Demo complete.");
    }
}
