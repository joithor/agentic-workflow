namespace Aw.Meta.Host.Commands;

/// <summary>The process a command runs in: its streams and environment; tests pass their own.</summary>
/// <param name="Out">Standard output.</param>
/// <param name="Err">Standard error.</param>
/// <param name="In">Standard input.</param>
/// <param name="Env">Reads an environment variable.</param>
internal sealed record ConsoleIo(TextWriter Out, TextWriter Err, TextReader In, Func<string, string?> Env)
{
    internal static ConsoleIo Standard() => new(Console.Out, Console.Error, Console.In, Environment.GetEnvironmentVariable);
}
