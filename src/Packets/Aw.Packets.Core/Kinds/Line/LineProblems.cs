using System.Globalization;
using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>What is wrong inside each typed file, as (where, message, fix); the rules wrap these with their ids.</summary>
internal static class LineProblems
{
    internal static IEnumerable<(string Message, string Fix)> Amends(LineManifest manifest, PhaseFile phase, bool amendment)
    {
        if (amendment && phase.Amends is null)
        {
            yield return ($"id '{manifest.Id.Value}' ends -a<n>, an amendment, but phase.json has no 'amends'.", "Add amends { parent, paths, reason, at }, or drop the suffix.");
        }

        if (!amendment && phase.Amends is not null)
        {
            yield return ($"phase.json has 'amends', but id '{manifest.Id.Value}' doesn't end -a<n>.", "Name an amended packet <line>-<phase>-a<n>.");
        }

        if (phase.Amends is { } amends)
        {
            if (amends.Paths.Count == 0)
            {
                yield return ("amends.paths is empty; an amendment grants at least one path.", "List the granted paths.");
            }

            foreach (var path in amends.Paths.Where(static p => ScopeGlob.Parse(p) is null))
            {
                yield return ($"amends.paths '{path}' isn't a glob or a safe 'new: <relative path>'.", "Use the write-scope syntax of allowedFiles.");
            }

            if (string.IsNullOrWhiteSpace(amends.Reason))
            {
                yield return ("amends.reason is empty.", "Record why the owner allowed the paths.");
            }
        }
    }

    internal static IEnumerable<(string Where, string Message, string Fix)> AcceptanceShape(AcceptanceFile file)
    {
        if (file.SchemaVersion != PacketReader.SchemaVersion)
        {
            yield return ("schemaVersion", $"schemaVersion is {file.SchemaVersion.ToString(CultureInfo.InvariantCulture)}; this build reads {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.", "Set schemaVersion to 1.");
        }

        for (var i = 0; i < file.Items.Count; i++)
        {
            var item = file.Items[i];
            var where = $"items[{i.ToString(CultureInfo.InvariantCulture)}]";
            if (string.IsNullOrWhiteSpace(item.Text))
            {
                yield return (where, $"{where} has no text.", "Give every item its text; ids and texts never change.");
            }
        }
    }

    internal static IEnumerable<(string Where, string Message, string Fix)> Chain(AcceptanceFile file, bool allowPassing)
    {
        foreach (var group in file.Items.GroupBy(static i => i.Id.Value, StringComparer.Ordinal).Where(static g => g.Skip(1).Any()))
        {
            yield return ($"item {group.Key}", $"item '{group.Key}' appears {group.Count().ToString(CultureInfo.InvariantCulture)} times.", "Give every item its own id.");
        }

        foreach (var item in file.Items.Where(static i => i.Status == AcceptanceStatus.Passing))
        {
            if (!allowPassing)
            {
                yield return ($"item {item.Id.Value}", $"item '{item.Id.Value}' is passing; only the proof pack flips an item to passing (LC-2).", "Set its status to failing.");
            }
            else if (item.Evidence.Count == 0)
            {
                yield return ($"item {item.Id.Value}", $"item '{item.Id.Value}' is passing with no evidence.", "Add the evidence { kind, path, sha256, commit, at }, or set the status back to failing.");
            }
        }
    }

    internal static IEnumerable<(string Where, string Message, string Fix)> Tasks(TaskPlan plan)
    {
        if (plan.SchemaVersion != PacketReader.SchemaVersion)
        {
            yield return ("schemaVersion", $"schemaVersion is {plan.SchemaVersion.ToString(CultureInfo.InvariantCulture)}; this build reads {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.", "Set schemaVersion to 1.");
        }

        if (plan.Tasks.Count > PacketReader.MaxTasks)
        {
            yield return ("tasks", $"tasks.json holds {plan.Tasks.Count.ToString(CultureInfo.InvariantCulture)} tasks; a packet holds at most {PacketReader.MaxTasks.ToString(CultureInfo.InvariantCulture)}.", "Split the change.");
        }
    }

    internal static IEnumerable<(string Where, string Message, string Fix)> Phase(PhaseFile phase)
    {
        if (phase.SchemaVersion != PacketReader.SchemaVersion)
        {
            yield return ("schemaVersion", $"schemaVersion is {phase.SchemaVersion.ToString(CultureInfo.InvariantCulture)}; this build reads {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.", "Set schemaVersion to 1.");
        }

        if (string.IsNullOrWhiteSpace(phase.Goal))
        {
            yield return ("goal", "goal is empty.", "State what the phase achieves.");
        }

        if (string.IsNullOrWhiteSpace(phase.Base.Ref))
        {
            yield return ("base.ref", "base.ref is empty.", "Name the branch (default-branch) or the dependency phase (dependency-head).");
        }

        if (phase.Base.Rule == BaseRule.DependencyHead && !phase.Deps.Contains(phase.Base.Ref, StringComparer.Ordinal))
        {
            yield return ("base.ref", $"base.ref '{phase.Base.Ref}' is not one of deps, but the rule is dependency-head.", "Point base.ref at a phase in deps.");
        }

        if (Repeats(phase.Deps))
        {
            yield return ("deps", "deps lists a phase twice.", "List each dependency once.");
        }

        foreach (var problem in PhaseScope(phase))
        {
            yield return problem;
        }
    }

    private static IEnumerable<(string Where, string Message, string Fix)> PhaseScope(PhaseFile phase)
    {
        if (phase.AllowedFiles.Count == 0)
        {
            yield return ("allowedFiles", "allowedFiles is empty; a phase that may write nothing builds nothing.", "List the globs and 'new: <path>' entries the phase may write.");
        }

        foreach (var entry in phase.AllowedFiles.Where(static e => ScopeGlob.Parse(e) is null))
        {
            yield return ("allowedFiles", $"allowedFiles '{entry}' isn't a glob or a safe 'new: <relative path>'.", "Use a relative glob, or 'new: <path>' for a file the phase creates.");
        }

        foreach (var change in phase.TestChange.Where(static t => string.IsNullOrWhiteSpace(t.Path) || string.IsNullOrWhiteSpace(t.Reason)))
        {
            yield return ("testChange", $"testChange '{change.Path}' needs a path and a reason.", "Name the existing test file and why it changes (BLD-TEST-001).");
        }
    }

    internal static IEnumerable<(string Where, string Message, string Fix)> Holdout(PhaseHoldout holdout)
    {
        if (holdout.SchemaVersion != PacketReader.SchemaVersion)
        {
            yield return ("schemaVersion", $"schemaVersion is {holdout.SchemaVersion.ToString(CultureInfo.InvariantCulture)}; this build reads {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.", "Set schemaVersion to 1.");
        }

        if (holdout.Count < 0)
        {
            yield return ("count", "count is negative.", "Record how many scenarios the set holds.");
        }
    }

    internal static IEnumerable<(string Where, string Message, string Fix)> PullRequest(PullRequestFile pr)
    {
        if (pr.SchemaVersion != PacketReader.SchemaVersion)
        {
            yield return ("schemaVersion", $"schemaVersion is {pr.SchemaVersion.ToString(CultureInfo.InvariantCulture)}; this build reads {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.", "Set schemaVersion to 1.");
        }

        foreach (var (name, value) in new[] { ("base", pr.Base), ("headBranch", pr.HeadBranch), ("title", pr.Title) })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield return (name, $"{name} is empty.", $"Record the PR's {name}.");
            }
        }

        if (Repeats(pr.Labels))
        {
            yield return ("labels", "labels lists a label twice.", "List each label once.");
        }
    }

    /// <summary>True when a list holds the same value twice.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="items">The list.</param>
    /// <returns>True for a repeat.</returns>
    internal static bool Repeats<T>(IEnumerable<T> items)
    {
        var seen = new HashSet<T>();
        return items.Any(i => !seen.Add(i));
    }
}
