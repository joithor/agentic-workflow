namespace Aw.Meta.Host.Commands;

/// <summary>The streams a command reads and writes; tests pass their own.</summary>
/// <param name="Out">Standard output.</param>
/// <param name="Err">Standard error.</param>
/// <param name="In">Standard input.</param>
internal sealed record ConsoleIo(TextWriter Out, TextWriter Err, TextReader In)
{
    internal static ConsoleIo Standard() => new(Console.Out, Console.Error, Console.In);
}
