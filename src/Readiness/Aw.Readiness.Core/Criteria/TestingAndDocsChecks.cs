using System.Globalization;

namespace Aw.Readiness.Core.Criteria;

/// <summary>Testing and Documentation criteria for the C# template.</summary>
internal static class TestingAndDocsChecks
{
    private const string Agents = "AGENTS.md";
    private const string OneCommand = "dotnet test --solution AgenticWorkflow.slnx";

    internal static IEnumerable<CatalogueEntry> Entries()
    {
        yield return CatalogueEntry.Of(new("TS-1", Pillar.Testing, 1), "Unit tests exist (unit lane)",
            "add a test with Trait(\"Lane\", \"unit\") under tests/ (spec §4b.5)", c => Lane(c, "unit", "Unit"));
        yield return CatalogueEntry.Of(new("TS-2", Pillar.Testing, 3), "Integration tests exist (integration lane)",
            "add a test with Trait(\"Lane\", \"integration\") under tests/ (real SQLite or WebApplicationFactory)", c => Lane(c, "integration", "Integration"));
        yield return CatalogueEntry.Of(new("TS-3", Pillar.Testing, 3), "Tests runnable locally (one command)",
            $"list '{OneCommand}' in AGENTS.md", c => Verdict.When(c.Contains(Agents, OneCommand),
                $"AGENTS.md lists '{OneCommand}'", $"AGENTS.md doesn't list '{OneCommand}'"));
        yield return CatalogueEntry.Of(new("TS-4", Pillar.Testing, 4), "Fast CI feedback (PR CI, affected areas, lane budgets)",
            "run dotnet test on pull_request with changed-area detection and the per-test lane cap (Aw.LaneCap.Testing)", FastCi);
        yield return CatalogueEntry.Of(new("TS-5", Pillar.Testing, 4), "Flaky test detection (quarantine)",
            "add the quarantine job (spec §4e B11: in on 2 flaky failures in 7 days, out after 20 green runs)",
            c => Verdict.When(c.WorkflowsContaining("quarantine").Count > 0, RepoContext.Named(c.WorkflowsContaining("quarantine"), "quarantine job in"),
                "no workflow runs a quarantine job"));
        yield return CatalogueEntry.Of(new("DOC-1", Pillar.Documentation, 2), "AGENTS.md (covers the C# build)",
            "keep AGENTS.md at the root with the dotnet commands", c => Verdict.When(c.Contains(Agents, "dotnet"),
                "AGENTS.md present and lists dotnet commands", c.Has(Agents) ? "AGENTS.md doesn't mention the C# build (dotnet)" : "no AGENTS.md"));
        yield return CatalogueEntry.Of(new("DOC-2", Pillar.Documentation, 1), "README",
            "add README.md at the root", c => Verdict.When(c.Has("README.md"), "README.md present", "no README.md"));
        yield return CatalogueEntry.Of(new("DOC-3", Pillar.Documentation, 4), "Documentation freshness checked automatically",
            "run scripts/sync-rules.sh --check in CI (and, W8, the AGENTS.md command executor)", DocFreshness);
    }

    private static Verdict Lane(RepoContext c, string trait, string attribute)
    {
        var files = c.FilesContaining("tests", ".cs", $"Trait(\"Lane\", \"{trait}\")")
            .Concat(c.FilesContaining("tests", ".cs", $"[Lane(TestLane.{attribute})]"))
            .Distinct(StringComparer.Ordinal).ToList();
        return Verdict.When(files.Count > 0, string.Create(CultureInfo.InvariantCulture, $"{files.Count} test files in the {trait} lane"),
            $"no test under tests/ carries the {trait} lane");
    }

    private static Verdict FastCi(RepoContext c)
    {
        string[] needles = ["pull_request", "dotnet test", "changed-areas", "LaneCap"];
        var missing = needles.Where(n => c.WorkflowsContaining(n).Count == 0).ToList();
        return Verdict.When(missing.Count == 0,
            "CI is configured to run dotnet test on pull_request with changed-area detection and the per-test lane cap (config only: no run or its time is checked)",
            $"no workflow has: {string.Join(", ", missing)}");
    }

    private static Verdict DocFreshness(RepoContext c)
    {
        var workflows = c.WorkflowsContaining("sync-rules.sh --check");
        return Verdict.When(workflows.Count > 0,
            $"{RepoContext.Named(workflows, "sync-rules --check in")}; the AGENTS.md command executor (W8) isn't built yet",
            "no workflow runs scripts/sync-rules.sh --check");
    }
}
