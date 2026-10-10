namespace Aw.Readiness.Testing;

/// <summary>The smallest checkout that passes every local criterion of the C# template scope, file by file. Each
/// criterion test removes or edits one file and expects that one criterion to flip.</summary>
internal static class TemplateTree
{
    /// <summary>The CI workflow every CI criterion reads.</summary>
    public const string Workflow = ".github/workflows/ci.yml";

    /// <summary>A passing tree.</summary>
    /// <returns>The tree.</returns>
    public static InMemoryRepoTree Ready() => new InMemoryRepoTree()
        .With("readiness.yaml", "version: 1\nrepo: agentic-workflow\nring: 0\nscope: csharp-template\ntargets:\n  - ring: 0\n    level: 4\n")
        .With("README.md", "# Repo\n\nOpen PRs with the `gh` CLI.\n")
        .With("AGENTS.md", "dotnet build -warnaserror\ndotnet test --solution AgenticWorkflow.slnx\ndotnet format --verify-no-changes\n")
        .With("global.json", "{ \"sdk\": { \"version\": \"10.0.401\", \"rollForward\": \"disable\" } }")
        .With(".editorconfig", "[*.cs]\ndotnet_diagnostic.CA1031.severity = error\n")
        .With("Directory.Build.props",
            "<Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>")
        .With("Directory.Packages.props",
            "<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally><GlobalPackageReference Include=\"Microsoft.CodeAnalysis.NetAnalyzers\" Version=\"[10.0.100]\" />")
        .With("src/Aw.Kernel/Aw.Kernel.csproj", "<Project />")
        .With("src/Aw.Kernel/packages.lock.json", "{}")
        .With("tests/Aw.Kernel.Tests/Aw.Kernel.Tests.csproj", "<Project />")
        .With("tests/Aw.Kernel.Tests/packages.lock.json", "{}")
        .With("tests/Aw.Kernel.Tests/ResultTests.cs", "[Fact, Trait(\"Lane\", \"unit\")]")
        .With("tests/Aw.Kernel.Tests/StoreTests.cs", "[Lane(TestLane.Integration)]")
        .With(".pre-commit-config.yaml", "repos: []\n")
        .With("setup.sh", "#!/usr/bin/env bash\n")
        .With(".devcontainer/devcontainer.json", "{}")
        .With(".env.example", "AW_STATE_DIR=\n")
        .With("src/Aw.Kernel.Telemetry/Aw.Kernel.Telemetry.csproj", "<Project />")
        .With("src/Aw.Kernel.Telemetry/JsonLinesLoggerProvider.cs", "sealed class JsonLinesLoggerProvider : ILoggerProvider")
        .With("src/Aw.Kernel.Telemetry/TelemetryRuntime.cs", ".AddSource(pattern) .AddMeter(pattern)")
        .With(".github/CODEOWNERS", "* @owner\n")
        .With(".github/dependabot.yml", "updates:\n  - package-ecosystem: nuget\n")
        .With(".github/ISSUE_TEMPLATE/bug.yml", "name: Bug\n")
        .With(".github/pull_request_template.md", "## Evidence\n")
        .With(".github/labels.yml", "- name: \"type:bug\"\n- name: \"area:kernel\"\n- name: \"size:S\"\n- name: \"ring:0\"\n")
        .With(Workflow,
            "on:\n  pull_request:\njobs:\n  x:\n    steps:\n      - run: bash scripts/ci/changed-areas.sh\n      - run: dotnet test --solution AgenticWorkflow.slnx\n"
            + "      - run: dotnet run --project tests/Aw.LaneCap.Testing\n      - run: scripts/sync-rules.sh --check\n      - run: bash scripts/ci/quarantine.sh\n")
        .With("docs/readiness/product-signals.md", "# Product signals\n");
}
