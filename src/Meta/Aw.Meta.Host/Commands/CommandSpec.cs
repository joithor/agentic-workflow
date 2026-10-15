namespace Aw.Meta.Host.Commands;

/// <summary>What a command accepts. Every command also takes the --json flag.</summary>
/// <param name="Usage">The command's usage line.</param>
/// <param name="Required">Options with a value that must be given.</param>
/// <param name="Optional">Options with a value that may be given.</param>
/// <param name="Flags">Boolean flags (besides --json).</param>
/// <param name="Positionals">How many positional arguments it takes.</param>
internal sealed record CommandSpec(string Usage, string[] Required, string[] Optional, string[] Flags, int Positionals = 0)
{
    internal const string JsonFlag = "json";

    internal static CommandSpec None(string usage) => new(usage, [], [], []);
}
