using System.Globalization;
using Aw.BugFix.Core;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Infrastructure;

/// <summary>The git CLI on PATH.</summary>
internal sealed class GitProcess : IGit
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(2);

    public Result<string> Head(string directory) => Value(directory, "rev-parse", "HEAD");

    public Result<string> Branch(string directory) => Value(directory, "rev-parse", "--abbrev-ref", "HEAD");

    // Untracked files count: a new fixture or helper can change what a test does while the commit stays the same.
    public Result<bool> IsClean(string directory) =>
        Value(directory, "status", "--porcelain", "--untracked-files=all").Map(static s => s.Length == 0);

    public Result<string> Root(string directory) => Value(directory, "rev-parse", "--show-toplevel");

    public Result<bool> IsCommittedUnchanged(string root, string file)
    {
        var committed = Run(root, "rev-parse", "--verify", "--quiet", $"HEAD:{file}");
        if (committed.ExitCode != 0)
        {
            return false;
        }

        return Value(root, "hash-object", "--", file).Map(working => working == committed.Output.Trim());
    }

    public Result<bool> IsAncestor(string directory, string ancestor, string commit)
    {
        var run = Run(directory, "merge-base", "--is-ancestor", ancestor, commit);
        return run.ExitCode switch
        {
            0 => true,
            1 => false,
            _ => Failed(run, "merge-base"),
        };
    }

    public Result<bool> SameTree(string directory, string first, string second) =>
        Value(directory, "rev-parse", $"{first}^{{tree}}").Bind(a => Value(directory, "rev-parse", $"{second}^{{tree}}").Map(b => a == b));

    // -z: raw (unquoted) paths; --no-renames: a moved test reports its old path too.
    public Result<IReadOnlyList<string>> ChangedFiles(string directory, string from, string to)
    {
        var run = Run(directory, "diff", "--name-only", "--no-renames", "-z", from, to);
        return run.ExitCode == 0
            ? Results.Success<IReadOnlyList<string>>(run.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            : Failed(run, "diff");
    }

    // Lock files and build output are noise to judge; without the full diff judge still has the stat.
    public Result<CommitDiff> Diff(string directory, string from, string to) =>
        Value(directory, "diff", "--stat", from, to).Map(stat =>
        {
            var full = Run(directory, "diff", from, to, "--", ".", ":(exclude)package-lock.json", ":(exclude)**/dist/**");
            return new CommitDiff(stat, full.ExitCode == 0 ? full.Output : null);
        });

    private static Result<string> Value(string directory, params string[] args)
    {
        var run = Run(directory, args);
        return run.ExitCode == 0 ? run.Output.TrimEnd('\n') : Failed(run, args[0]);
    }

    private static ProcessResult Run(string directory, params string[] args)
    {
        var start = Processes.Start("git", ["-C", directory, "-c", "core.quotepath=off", .. args]);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        return Processes.Capture(start, _timeout);
    }

    private static Error Failed(ProcessResult run, string command)
    {
        var reason = run.Error.Trim().Split('\n')[0];
        return BugFixErrors.GitFailed.With($"git {command} failed: {(reason.Length > 0 ? reason : string.Create(CultureInfo.InvariantCulture, $"exit {run.ExitCode}"))}");
    }
}
