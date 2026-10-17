using System.Collections;
using System.Globalization;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Checks;

/// <summary>
/// PKT-SIZE-001 (line spec LN-PKT3): every task fits its size class's limits from the sizes file: the files its write
/// scope covers (the base commit's files its globs match, plus the paths it declares new, each once), its acceptance
/// items (held-out ones included) and its budget. A task's budget field or a limit the file leaves out is not compared.
/// Unchecked without a sizes file, and when the base commit can't be read (the files can't be counted).
/// </summary>
internal static class SizeChecks
{
    private const string Fix = "Split the task into smaller ones, or give it a larger size class; the limits come from the sizes file (the profile's workflow.plan.sizes).";

    internal static void Run(Packet packet, BaseTree tree, SizesFile? sizes, CheckList checks)
    {
        if (sizes is null)
        {
            checks.Unchecked(PacketRules.SizeBudget, "no sizes file was given (validate --sizes <file>), so each task's size and budget are recorded but not compared");
            return;
        }

        if (tree.Files is not { } files)
        {
            checks.Unchecked(PacketRules.SizeBudget, "skipped: the base commit can't be read (PKT-BASE-001), so the files a task's write scope covers can't be counted");
            return;
        }

        var parsed = new Dictionary<string, ScopeGlob?>(StringComparer.Ordinal);
        foreach (var entry in packet.Tasks.Tasks.SelectMany(static t => t.WriteScope).Distinct(StringComparer.Ordinal))
        {
            parsed[entry] = ScopeGlob.Parse(entry);
        }

        var index = new PathIndex(files, parsed.Values.Select(static g => g?.NewPath).OfType<string>());
        checks.Rule(
            PacketRules.SizeBudget,
            "every task fits its size class's files, items and budget",
            packet.Tasks.Tasks.Select(t => (Task: t, Problems: Problems(t, sizes.Sizes.Of(t.Size), FileCount(t, parsed, index))))
                .Where(static x => x.Problems.Count > 0)
                .Select(static x => ($"task {x.Task.Id.Value}", $"is size {Name(x.Task.Size)} but {string.Join("; ", x.Problems)}", Fix)));
    }

    private static int FileCount(PacketTask task, Dictionary<string, ScopeGlob?> parsed, PathIndex index)
    {
        var bits = new BitArray(index.Paths.Count);
        foreach (var glob in task.WriteScope.Select(e => parsed[e]).OfType<ScopeGlob>())
        {
            index.MatchInto(glob, bits);
        }

        var count = 0;
        for (var i = 0; i < bits.Length; i++)
        {
            if (bits[i])
            {
                count++;
            }
        }

        return count;
    }

    private static List<string> Problems(PacketTask task, SizeLimit limit, int files)
    {
        var size = Name(task.Size);
        var problems = new List<string>();
        Over(problems, size, "its write scope covers", "files", (files, limit.Files));
        Over(problems, size, "it has", "acceptance items", (task.Acceptance.Count, limit.Items));
        Over(problems, size, "its budget asks for", "tokens", (task.Budget.Tokens, limit.Budget.Tokens));
        Over(problems, size, "its budget asks for", "turns", (task.Budget.Turns, limit.Budget.Turns));
        Over(problems, size, "its budget asks for", "wall minutes", (task.Budget.WallMinutes, limit.Budget.WallMinutes));
        return problems;
    }

    private static void Over(List<string> problems, string size, string what, string unit, (long? Asked, long? Allowed) amount)
    {
        if (amount is { Asked: { } have, Allowed: { } most } && have > most)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"{what} {have} {unit}, and {size} allows at most {most}"));
        }
    }

    internal static string Name(SizeClass size) => size switch
    {
        SizeClass.ExtraSmall => "XS",
        SizeClass.Small => "S",
        SizeClass.Medium => "M",
        SizeClass.Large => "L",
        SizeClass.ExtraLarge => "XL",
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Not a size class."),
    };
}
