using Aw.BugFix.Core.Errors;
using Aw.Kernel;

namespace Aw.BugFix.Core.Model;

/// <summary>The check that reproduced the bug, frozen at advance reproduce: every later run must execute exactly it.</summary>
/// <param name="Kind">A regression test or a ui-evidence script.</param>
/// <param name="Path">The check file, relative to the repository root, with '/'.</param>
/// <param name="Sha256">The check file's SHA-256 at the baseline.</param>
/// <param name="Command">The test command (argv) of the baseline run; null for a ui-evidence check.</param>
/// <param name="Directory">The working directory of the baseline run, relative to the repository root ("" for the root).</param>
/// <param name="CommandFiles">Repository files the baseline command names besides the check (its runner, wrapper scripts): a fix may not change them without approval.</param>
public sealed record FrozenCheck(CheckKind Kind, string Path, string Sha256, IReadOnlyList<string>? Command, string Directory, IReadOnlyList<string> CommandFiles)
{
    /// <summary>Accepts a later run only if it executed this check: same kind, a verdict (not broken), and for a test the same command in the same directory.</summary>
    /// <param name="record">The run.</param>
    /// <returns>The run, or AW-BUGFIX-014.</returns>
    public Result<RunRecord> Accepts(RunRecord record)
    {
        if (record.Kind != Kind)
        {
            return BugFixErrors.RunRefused.With($"the baseline check was a {Names.Of(Kind)}, this evidence is a {Names.Of(record.Kind)}");
        }

        if (record.Outcome == RunOutcome.Broken)
        {
            return BugFixErrors.RunRefused.With("the run is broken (it didn't start, timed out, or only had broken steps); repair it and re-run, a broken run is not a verdict on the fix");
        }

        if (Kind != CheckKind.Test)
        {
            return record;
        }

        return (record.Command ?? []).SequenceEqual(Command ?? [], StringComparer.Ordinal) && record.Directory == Directory
            ? record
            : BugFixErrors.RunRefused.With(
                $"the test command `{string.Join(' ', record.Command ?? [])}` in '{record.Directory}' differs from the baseline's `{string.Join(' ', Command ?? [])}` in '{Directory}'");
    }
}
