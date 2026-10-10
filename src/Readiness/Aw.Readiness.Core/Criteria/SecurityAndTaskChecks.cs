using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.Core.Criteria;

/// <summary>Security, Task Discovery and Product and Experimentation criteria for the C# template.</summary>
internal static class SecurityAndTaskChecks
{
    private const string ProductSignals = "docs/readiness/product-signals.md";

    internal static IEnumerable<CatalogueEntry> Entries()
    {
        yield return CatalogueEntry.Of("SEC-1", Pillar.Security, 2, "Branch protection on the default branch",
            "protect main with a ruleset: required checks, linear history, no force pushes, empty bypass list (spec §4d)", c => Host(c.Host.BranchProtection));
        yield return CatalogueEntry.Of("SEC-2", Pillar.Security, 3, "Secret scanning and push protection",
            "turn on GitHub secret scanning and push protection (spec §5 L3)", c => Host(c.Host.SecretScanning));
        yield return CatalogueEntry.Of("SEC-3", Pillar.Security, 3, "CODEOWNERS",
            "add CODEOWNERS for the protected modules, backed by a required check (spec §4d)", c => Present(c, ".github/CODEOWNERS", "CODEOWNERS", "docs/CODEOWNERS"));
        yield return CatalogueEntry.Of("SEC-4", Pillar.Security, 3, "Dependency update bot covers the C# packages",
            "add a 'package-ecosystem: nuget' entry to .github/dependabot.yml (grouped, 14-day cooldown)", NuGetUpdates);
        yield return CatalogueEntry.Of("TD-1", Pillar.TaskDiscovery, 3, "Issue templates",
            "add issue forms under .github/ISSUE_TEMPLATE/", IssueTemplates);
        yield return CatalogueEntry.Of("TD-2", Pillar.TaskDiscovery, 3, "Issue labeling system (label scheme file)",
            "keep .github/labels.yml with the type:, area:, size: and ring: families (spec §5 L3)", LabelScheme);
        yield return CatalogueEntry.Of("TD-3", Pillar.TaskDiscovery, 3, "PR template",
            "add .github/pull_request_template.md with the evidence section", c => Present(c, ".github/pull_request_template.md", ".github/PULL_REQUEST_TEMPLATE.md", ".github/PULL_REQUEST_TEMPLATE"));
        yield return CatalogueEntry.Of("TD-4", Pillar.TaskDiscovery, 5, "Complex requirements decomposed into parallel execution",
            "build packets, workflow generation and stacked epics (factory addendum F0-F3)",
            _ => Verdict.Unknown("no observable artifact yet: frozen packets and generated workflows (addendum F0-F3) are not built"));
        yield return CatalogueEntry.Of("PE-1", Pillar.ProductAndExperimentation, 4, "Product analytics instrumentation",
            $"write {ProductSignals} mapping scorer and ledger telemetry to product analytics (W9)", c => Present(c, ProductSignals));
        yield return CatalogueEntry.Of("PE-2", Pillar.ProductAndExperimentation, 4, "Experiment infrastructure",
            $"list evolve's A/B and the judge's shadow modes as experiments in {ProductSignals} (W9)", c => Present(c, ProductSignals));
        yield return CatalogueEntry.Of("PE-3", Pillar.ProductAndExperimentation, 5, "Self-improving systems",
            "the readiness module proposes its own fixes weekly (spec §8)",
            _ => Verdict.Unknown("no observable artifact yet: the weekly readiness workflow (§8) is not built"));
    }

    private static Verdict Host(HostFact fact) => fact.Holds switch
    {
        true => Verdict.Pass(fact.Evidence),
        false => Verdict.Fail(fact.Evidence),
        null => Verdict.Unknown(fact.Evidence),
    };

    private static Verdict Present(RepoContext c, params string[] paths)
    {
        var found = c.FirstExisting(paths);
        return Verdict.When(found is not null, $"{found} present", $"no {string.Join(" or ", paths)}");
    }

    private static Verdict NuGetUpdates(RepoContext c)
    {
        var config = c.FirstExisting(".github/dependabot.yml", ".github/dependabot.yaml");
        return config is null
            ? Verdict.Fail("no .github/dependabot.yml")
            : Verdict.When(c.Contains(config, "package-ecosystem: nuget"), $"{config} updates nuget packages",
                $"{config} has no nuget ecosystem, so the C# packages get no update PRs");
    }

    private static Verdict IssueTemplates(RepoContext c)
    {
        var forms = c.Tree.Files(".github/ISSUE_TEMPLATE", "")
            .Where(f => !f.EndsWith("/config.yml", StringComparison.Ordinal) && (f.EndsWith(".yml", StringComparison.Ordinal) || f.EndsWith(".md", StringComparison.Ordinal)))
            .ToList();
        return Verdict.When(forms.Count > 0, RepoContext.Named(forms, "issue templates"), "no issue templates under .github/ISSUE_TEMPLATE/");
    }

    private static Verdict LabelScheme(RepoContext c)
    {
        const string scheme = ".github/labels.yml";
        string[] families = ["type:", "area:", "size:", "ring:"];
        var missing = families.Where(f => !c.Contains(scheme, $"\"{f}")).ToList();
        return !c.Has(scheme)
            ? Verdict.Fail($"no {scheme}")
            : Verdict.When(missing.Count == 0, $"{scheme} defines the type:, area:, size: and ring: families (applying them to the repo is the owner's)",
                $"{scheme} lacks the {string.Join(", ", missing)} families");
    }
}
