using Aw.Kernel;

namespace Aw.Readiness.App.Commands;

/// <summary>The 'check' command's arguments: [--json] [--root DIR] [--no-remote].</summary>
/// <param name="Root">The repo root.</param>
/// <param name="UseHost">False with --no-remote.</param>
/// <param name="Json">True with --json.</param>
internal sealed record CheckArgs(string Root, bool UseHost, bool Json)
{
    internal const string Usage = "usage: aw-readiness check [--json] [--root DIR] [--no-remote]";

    internal static Result<CheckArgs> Parse(IReadOnlyList<string> args, string currentDirectory) =>
        throw new NotSupportedException($"red: CheckArgs.Parse {args.Count} {currentDirectory}");
}
