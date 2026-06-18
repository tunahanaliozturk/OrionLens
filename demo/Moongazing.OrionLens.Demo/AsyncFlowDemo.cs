namespace Moongazing.OrionLens.Demo;

using Moongazing.OrionLens.Context;

/// <summary>
/// Demonstrates that the correlation id follows the logical async flow: it is the same before and
/// after an <c>await</c>, including after hopping threads via <see cref="Task.Yield"/> and
/// continuing on the thread pool. This is the whole point of the <c>AsyncLocal</c> backing store.
/// </summary>
internal static class AsyncFlowDemo
{
    public static async Task RunAsync()
    {
        DemoConsole.Header("2. The id flows across an await boundary");

        var context = CorrelationContext.Create("flow-4d8e1f60");

        using (OrionContext.BeginScope(context))
        {
            DemoConsole.Line($"Before await, thread {Environment.CurrentManagedThreadId,-3} sees id: {OrionContext.Current!.CorrelationId}");

            await Task.Yield();
            DemoConsole.Line($"After  yield, thread {Environment.CurrentManagedThreadId,-3} sees id: {OrionContext.Current!.CorrelationId}");

            await SimulateDownstreamWorkAsync();

            await Task.Run(() =>
                DemoConsole.Line($"In Task.Run, thread {Environment.CurrentManagedThreadId,-3} sees id: {OrionContext.Current!.CorrelationId}"));
        }
    }

    private static async Task SimulateDownstreamWorkAsync()
    {
        await Task.Delay(15);
        DemoConsole.Line($"After delay, thread {Environment.CurrentManagedThreadId,-3} sees id: {OrionContext.Current!.CorrelationId}");
    }
}
