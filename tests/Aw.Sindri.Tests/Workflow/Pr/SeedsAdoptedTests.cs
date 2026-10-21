namespace Aw.Sindri.Tests.Workflow.Pr;

/// <summary>
/// PB spec §9.3, PB-R: a seed's prompt cycle 1 (PB-S1) gates 'recipe approve', so every seed under recipes/sindri/seeds/pr/ needs an
/// adoption report beside it ('&lt;step&gt;.adopted.md') before it counts as adopted. This is a ratchet: a seed without a report must
/// be listed in the pending list, and a listed seed that has a report must come off the list, so the list only shrinks.
/// Today every seed is pending; PB-S1 writes the reports and empties the list.
/// </summary>
public sealed class SeedsAdoptedTests
{
    /// <summary>The seeds PB-S1 has not adopted yet. PB-S1 empties this list as it writes each report.</summary>
    private static readonly string[] _pendingAdoption = ["step.docs.update", "step.pr.ci-fix", "step.pr.fix-comment", "step.pr.reply-draft"];

    /// <summary>The agent Steps of the babysitter and docs sync (PB spec §9.1): each has a seed.</summary>
    private static readonly string[] _agentSteps = ["step.docs.update", "step.pr.ci-fix", "step.pr.fix-comment", "step.pr.reply-draft"];

    [Fact, Trait("Lane", "unit")]
    public void Every_agent_Step_of_the_babysitter_and_docs_sync_has_a_seed() =>
        Assert.Equal(_agentSteps.Order(StringComparer.Ordinal), Seeds().Order(StringComparer.Ordinal), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_seed_without_an_adoption_report_is_pending_and_one_with_a_report_is_not()
    {
        var dir = SeedDirectory();
        var withoutReport = Seeds().Where(s => !File.Exists(Path.Combine(dir, s + ".adopted.md"))).ToList();

        Assert.Equal(_pendingAdoption.Order(StringComparer.Ordinal), withoutReport.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    private static List<string> Seeds() => [.. Directory.GetFiles(SeedDirectory(), "*.md")
        .Select(Path.GetFileNameWithoutExtension)
        .OfType<string>()
        .Where(n => !n.EndsWith(".adopted", StringComparison.Ordinal))];

    private static string SeedDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "recipes", "sindri", "seeds", "pr");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("recipes/sindri/seeds/pr isn't above the test binaries.");
    }
}
