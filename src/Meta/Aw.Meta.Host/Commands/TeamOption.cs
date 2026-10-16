namespace Aw.Meta.Host.Commands;

/// <summary>
/// The global --team option. It is taken out of the arguments before a command parses them and becomes AW_META_TEAM for the run
/// (it wins over the variable), so every layer reads one source. team-init keeps its own --team, which names the team it writes.
/// </summary>
internal static class TeamOption
{
    private const string Name = "--team";

    /// <summary>Applies --team to a run's arguments and environment.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The arguments the command parses, the environment with AW_META_TEAM set from --team, and what is wrong with the option (empty when nothing).</returns>
    internal static (string[] Args, Func<string, string?> Environment, string Problem) Apply(string[] args, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environment);
        var at = Array.IndexOf(args, Name);
        if (at < 0)
        {
            return (args, environment, string.Empty);
        }

        if (at + 1 >= args.Length || args[at + 1].Length == 0 || args[at + 1].StartsWith("--", StringComparison.Ordinal))
        {
            return (args, environment, "--team needs one non-empty value");
        }

        var team = args[at + 1];
        var rest = args[0] == "team-init" ? args : [.. args[..at], .. args[(at + 2)..]];
        return (rest, name => name == "AW_META_TEAM" ? team : environment(name), string.Empty);
    }
}
