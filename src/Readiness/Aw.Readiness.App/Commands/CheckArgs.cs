using Aw.Kernel;
using Aw.Readiness.Core;

namespace Aw.Readiness.App.Commands;

/// <summary>The 'check' command's arguments: [--json] [--root DIR] [--no-remote].</summary>
/// <param name="Root">The repo root; "." (the current directory, resolved by the file-system adapter) by default.</param>
/// <param name="UseHost">False with --no-remote.</param>
/// <param name="Json">True with --json.</param>
internal sealed record CheckArgs(string Root, bool UseHost, bool Json)
{
    internal const string Usage = "usage: aw-readiness check [--json] [--root DIR] [--no-remote]";

    internal const string CurrentDirectory = ".";

    internal static Result<CheckArgs> Parse(IReadOnlyList<string> args)
    {
        var parsed = new CheckArgs(CurrentDirectory, UseHost: true, Json: false);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i++)
        {
            var flag = args[i];
            if (!seen.Add(flag))
            {
                return Refuse($"{flag} is given twice");
            }

            switch (flag)
            {
                case "--json":
                    parsed = parsed with { Json = true };
                    break;
                case "--no-remote":
                    parsed = parsed with { UseHost = false };
                    break;
                case "--root" when i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal):
                    parsed = parsed with { Root = args[++i] };
                    break;
                case "--root":
                    return Refuse("--root needs a directory");
                default:
                    return Refuse($"unknown argument '{flag}'");
            }
        }

        return parsed;
    }

    private static Error Refuse(string problem) => ReadinessErrors.Invalid.With($"{problem}; {Usage}");
}
