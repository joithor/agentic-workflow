using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri services …' parsed: which verb, the service it names, and its flags.</summary>
/// <param name="Verb">'list', 'run', 'pause' or 'resume'.</param>
/// <param name="Name">The service, or null for the list.</param>
/// <param name="Json">--json.</param>
/// <param name="NoStart">--no-start.</param>
/// <param name="Detach">--detach (run only).</param>
internal sealed record ServiceCommandLine(string Verb, string? Name, bool Json, bool NoStart, bool Detach)
{
    internal const string Usage = "services [--json] [--no-start] | services run <name> [--detach] [--json] [--no-start] | services pause|resume <name> [--no-start]";

    /// <summary>Reads the arguments after 'services'.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The command line, or SND-CLI-003 naming the usage.</returns>
    internal static Result<ServiceCommandLine> Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var flags = args.Where(static a => a.StartsWith("--", StringComparison.Ordinal)).ToList();
        var words = args.Where(static a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        var verb = words.Count == 0 ? "list" : words[0];
        var allowed = verb switch
        {
            "list" => (Words: 0, Flags: new[] { "--json", "--no-start" }),
            "run" => (1, ["--json", "--no-start", "--detach"]),
            "pause" or "resume" => (1, ["--no-start"]),
            _ => (-1, []),
        };
        var counted = verb == "list" ? words.Count : words.Count - 1;
        if (allowed.Words < 0 || counted != allowed.Words || !flags.TrueForAll(f => allowed.Flags.Contains(f, StringComparer.Ordinal)))
        {
            return SindriErrors.InvalidInput.With($"'{string.Join(' ', args)}' isn't a services command: {Usage}");
        }

        return new ServiceCommandLine(verb, allowed.Words == 1 ? words[1] : null, flags.Contains("--json", StringComparer.Ordinal),
            flags.Contains("--no-start", StringComparer.Ordinal), flags.Contains("--detach", StringComparer.Ordinal));
    }
}
