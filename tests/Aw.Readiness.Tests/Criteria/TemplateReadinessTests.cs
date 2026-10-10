using Aw.Readiness.Core;
using Aw.Readiness.Core.Criteria;
using Aw.Readiness.Core.Ports;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.Criteria;

public sealed class TemplateReadinessTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] _hostIds = ["BS-4", "SEC-1", "SEC-2", "TD-2"];

    private static readonly HostFacts _hostReady = new(
        new HostFact(Holds: true, Evidence: "ruleset 'main'"),
        new HostFact(Holds: true, Evidence: "secret scanning and push protection enabled"),
        new HostReleases([_now.AddDays(-1), _now.AddDays(-30), _now.AddDays(-89)], "3 releases"),
        new HostLabels(["type:bug", "area:kernel", "size:S", "ring:0"], "4 labels"));

    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_has_the_r0_baselines_34_criteria_with_unique_ids()
    {
        var criteria = TemplateReadiness.Criteria;

        Assert.Equal(34, criteria.Count);
        Assert.Equal(criteria.Count, criteria.Select(c => c.Id).Distinct().Count());
        Assert.All(criteria, c => Assert.InRange(c.Level, 1, 5));
        Assert.All(Enum.GetValues<Pillar>(), p => Assert.Contains(criteria, c => c.Pillar == p));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_ready_template_passes_every_criterion_but_the_two_with_no_artifact_yet()
    {
        var findings = TemplateReadiness.Evaluate(A.ReadyTemplate(), _hostReady, _now);

        Assert.Equal(TemplateReadiness.Criteria.Select(c => c.Id), findings.Select(f => f.Criterion.Id));
        var notPassing = findings.Where(f => f.State != CriterionState.Pass).Select(f => f.Criterion.Id.Value).ToList();
        Assert.Equal(["TD-4", "PE-3"], notPassing);
        Assert.All(findings, f => Assert.False(string.IsNullOrWhiteSpace(f.Evidence)));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SV-1", "Directory.Packages.props")]
    [InlineData("SV-2", "Directory.Build.props")]
    [InlineData("SV-3", ".editorconfig")]
    [InlineData("SV-4", ".pre-commit-config.yaml")]
    [InlineData("BS-1", "AGENTS.md")]
    [InlineData("BS-2", "src/Aw.Kernel/packages.lock.json")]
    [InlineData("BS-3", "README.md")]
    [InlineData("BS-5", "global.json")]
    [InlineData("TS-1", "tests/Aw.Kernel.Tests/ResultTests.cs")]
    [InlineData("TS-2", "tests/Aw.Kernel.Tests/StoreTests.cs")]
    [InlineData("TS-3", "AGENTS.md")]
    [InlineData("TS-4", TemplateTree.Workflow)]
    [InlineData("TS-5", TemplateTree.Workflow)]
    [InlineData("DOC-1", "AGENTS.md")]
    [InlineData("DOC-2", "README.md")]
    [InlineData("DOC-3", TemplateTree.Workflow)]
    [InlineData("DE-1", ".devcontainer/devcontainer.json")]
    [InlineData("DE-2", ".env.example")]
    [InlineData("DE-3", "setup.sh")]
    [InlineData("OBS-1", "src/Aw.Kernel.Telemetry/JsonLinesLoggerProvider.cs")]
    [InlineData("OBS-2", "src/Aw.Kernel.Telemetry/TelemetryRuntime.cs")]
    [InlineData("OBS-3", "src/Aw.Kernel.Telemetry/TelemetryRuntime.cs")]
    [InlineData("SEC-3", ".github/CODEOWNERS")]
    [InlineData("SEC-4", ".github/dependabot.yml")]
    [InlineData("TD-1", ".github/ISSUE_TEMPLATE/bug.yml")]
    [InlineData("TD-2", ".github/labels.yml")]
    [InlineData("TD-3", ".github/pull_request_template.md")]
    [InlineData("PE-1", "docs/readiness/product-signals.md")]
    [InlineData("PE-2", "docs/readiness/product-signals.md")]
    public void Removing_a_criterions_artifact_fails_it_with_its_fix(string id, string path)
    {
        var findings = TemplateReadiness.Evaluate(A.ReadyTemplate().Without(path), _hostReady, _now);

        var finding = findings.Single(f => f.Criterion.Id.Value == id);
        Assert.Equal(CriterionState.Fail, finding.State);
        Assert.False(string.IsNullOrWhiteSpace(finding.Criterion.Fix));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SV-2", "Directory.Build.props", "<Nullable>disable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors>")]
    [InlineData("BS-5", "global.json", "{ \"sdk\": { \"version\": \"10.0.401\", \"rollForward\": \"latestFeature\" } }")]
    [InlineData("SEC-4", ".github/dependabot.yml", "updates:\n  - package-ecosystem: npm\n")]
    [InlineData("TD-2", ".github/labels.yml", "- name: \"type:bug\"\n")]
    [InlineData("BS-2", "src/Aw.Kernel/Aw.Kernel.csproj", "<RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>")]
    [InlineData("BS-3", "README.md", "# Repo\n\nThough the highlights are good.\n")]
    public void A_weaker_setting_fails_the_criterion(string id, string path, string text)
    {
        var findings = TemplateReadiness.Evaluate(A.ReadyTemplate().With(path, text), _hostReady, _now);

        Assert.Equal(CriterionState.Fail, findings.Single(f => f.Criterion.Id.Value == id).State);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("- name: type:bug\n- name: area:kernel\n- name: size:S\n- name: ring:0\n")]
    [InlineData("- name: 'type:bug'\n- name: 'area:kernel'\n- name: 'size:S'\n- name: 'ring:0'\n")]
    public void A_label_scheme_with_plain_or_single_quoted_names_counts(string scheme)
    {
        var finding = TemplateReadiness.Evaluate(A.ReadyTemplate().With(".github/labels.yml", scheme), _hostReady, _now)
            .Single(f => f.Criterion.Id.Value == "TD-2");

        Assert.Equal(CriterionState.Pass, finding.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void Td2_fails_when_the_scheme_is_not_applied_to_the_repo()
    {
        var host = _hostReady with { Labels = new HostLabels(["bug", "documentation"], "2 labels") };

        var finding = TemplateReadiness.Evaluate(A.ReadyTemplate(), host, _now).Single(f => f.Criterion.Id.Value == "TD-2");

        Assert.Equal(CriterionState.Fail, finding.State);
        Assert.Contains("not applied", finding.Evidence, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_project_without_a_lock_file_is_named_in_the_evidence()
    {
        var tree = A.ReadyTemplate().With("src/Aw.Extra/Aw.Extra.csproj", "<Project />");

        var finding = TemplateReadiness.Evaluate(tree, _hostReady, _now).Single(f => f.Criterion.Id.Value == "BS-2");

        Assert.Equal(CriterionState.Fail, finding.State);
        Assert.Contains("src/Aw.Extra/Aw.Extra.csproj", finding.Evidence, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_project_that_opts_out_of_lock_files_is_not_counted_and_is_named()
    {
        var tree = A.ReadyTemplate().With(
            "src/Aw.Kernel.AotSmoke/Aw.Kernel.AotSmoke.csproj", "<RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>");

        var finding = TemplateReadiness.Evaluate(tree, _hostReady, _now).Single(f => f.Criterion.Id.Value == "BS-2");

        Assert.Equal(CriterionState.Pass, finding.State);
        Assert.Contains("opted out (not in the solution): src/Aw.Kernel.AotSmoke/Aw.Kernel.AotSmoke.csproj", finding.Evidence, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Unconfirmed_lists_only_unread_criteria_from_the_reached_level_up_to_the_target()
    {
        Finding[] findings = [
            A.Finding("SEC-1", CriterionState.Unknown, Pillar.Security, level: 2) with { Unread = true },
            A.Finding("SEC-2", CriterionState.Unknown, Pillar.Security, level: 3) with { Unread = true },
            A.Finding("BS-4", CriterionState.Unknown, Pillar.BuildSystem, level: 4) with { Unread = true },
        ];

        var unconfirmed = TemplateReadiness.Unconfirmed(findings, new ReadinessTarget(0, 4), reached: 3);

        Assert.Equal(["SEC-2"], [.. unconfirmed.Select(id => id.Value)]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Host_criteria_are_unknown_when_the_host_could_not_be_read()
    {
        var findings = TemplateReadiness.Evaluate(A.ReadyTemplate(), HostFacts.Unknown("--no-remote"), _now);

        foreach (var id in _hostIds)
        {
            var finding = findings.Single(f => f.Criterion.Id.Value == id);
            Assert.Equal(CriterionState.Unknown, finding.State);
            Assert.True(finding.Unread);
            Assert.Contains("--no-remote", finding.Evidence, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Host_criteria_fail_on_what_the_host_reports()
    {
        var host = new HostFacts(
            new HostFact(Holds: false, Evidence: "not protected"),
            new HostFact(Holds: false, Evidence: "secret scanning disabled"),
            new HostReleases([_now.AddDays(-1), _now.AddDays(-91), _now.AddDays(-200)], "3 releases"),
            new HostLabels(["bug", "type:bug"], "2 labels"));

        var findings = TemplateReadiness.Evaluate(A.ReadyTemplate(), host, _now);

        Assert.All(_hostIds, id =>
            Assert.Equal(CriterionState.Fail, findings.Single(f => f.Criterion.Id.Value == id).State));
        Assert.Contains("1 release", findings.Single(f => f.Criterion.Id.Value == "BS-4").Evidence, StringComparison.Ordinal);
    }
}
