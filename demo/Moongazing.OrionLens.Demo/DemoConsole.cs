namespace Moongazing.OrionLens.Demo;

/// <summary>Tiny console helpers so each feature demo prints a consistent, readable block.</summary>
internal static class DemoConsole
{
    public static void Header(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 72));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 72));
    }

    public static void Line(string message) => Console.WriteLine("  " + message);

    public static void Bullet(string message) => Console.WriteLine("   - " + message);
}
