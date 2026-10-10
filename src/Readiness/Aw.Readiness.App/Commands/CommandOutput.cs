namespace Aw.Readiness.App.Commands;

/// <summary>Usage errors: printed to stderr, exit 2.</summary>
internal static class CommandOutput
{
    internal static int Usage(string message)
    {
        Console.Error.WriteLine($"usage: {message}");
        return 2;
    }
}
