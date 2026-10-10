using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Readiness.Core.Criteria;

/// <summary>Style and Validation and Build System criteria for the C# template.</summary>
internal static partial class StyleAndBuildChecks
{
    private const string Packages = "Directory.Packages.props";
    private const string BuildProps = "Directory.Build.props";
    private const string Agents = "AGENTS.md";

    internal static IEnumerable<CatalogueEntry> Entries()
    {
        yield return CatalogueEntry.Of("SV-1", Pillar.StyleAndValidation, 1, "Linter configured (Roslyn analyzers)",
            "add Microsoft.CodeAnalysis.NetAnalyzers as a GlobalPackageReference in Directory.Packages.props (spec §4b.6)", Linter);
        yield return CatalogueEntry.Of("SV-2", Pillar.StyleAndValidation, 1, "Type checker strict (nullable, warnings as errors)",
            "set <Nullable>enable</Nullable> and <TreatWarningsAsErrors>true</TreatWarningsAsErrors> in Directory.Build.props", Strict);
        yield return CatalogueEntry.Of("SV-3", Pillar.StyleAndValidation, 1, "Code formatter (dotnet format over .editorconfig)",
            "keep .editorconfig at the root and list 'dotnet format --verify-no-changes' in AGENTS.md", Formatter);
        yield return CatalogueEntry.Of("SV-4", Pillar.StyleAndValidation, 2, "Pre-commit hooks (tracked config)",
            "commit a hook config (.pre-commit-config.yaml, .githooks/, lefthook.yml or .husky/) that runs format and the scrubber", PreCommit);
        yield return CatalogueEntry.Of("BS-1", Pillar.BuildSystem, 2, "Build command documented",
            "list 'dotnet build -warnaserror' in AGENTS.md", c => Documented(c, "dotnet build"));
        yield return CatalogueEntry.Of("BS-2", Pillar.BuildSystem, 2, "Dependencies pinned (central versions, lock files)",
            "turn on central package management and lock files, then run a restore and commit every packages.lock.json", Pinned);
        yield return CatalogueEntry.Of("BS-3", Pillar.BuildSystem, 2, "VCS CLI tools documented",
            "say in README.md or AGENTS.md that PRs and issues go through the gh CLI", VcsCli);
        yield return CatalogueEntry.Of("BS-4", Pillar.BuildSystem, 4, "Regular deployment frequency (≥3 releases in 90 days)",
            "promote a stable channel at least three times in 90 days (spec §5, W8)", Releases);
        yield return CatalogueEntry.Of("BS-5", Pillar.BuildSystem, 2, "Runtime version pinned (global.json)",
            "pin the SDK in global.json with \"rollForward\": \"disable\"", RuntimePinned);
    }

    private static Verdict Linter(RepoContext c) => Verdict.When(
        c.Contains(Packages, "GlobalPackageReference Include=\"Microsoft.CodeAnalysis.NetAnalyzers\"") && c.Contains(".editorconfig", "dotnet_diagnostic."),
        "NetAnalyzers is a GlobalPackageReference; .editorconfig sets analyzer severities",
        "no NetAnalyzers GlobalPackageReference in Directory.Packages.props, or no dotnet_diagnostic severities in .editorconfig");

    private static Verdict Strict(RepoContext c) => Verdict.When(
        c.Contains(BuildProps, "<Nullable>enable</Nullable>") && c.Contains(BuildProps, "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>"),
        "Directory.Build.props: Nullable=enable, TreatWarningsAsErrors=true",
        "Directory.Build.props lacks Nullable=enable or TreatWarningsAsErrors=true");

    private static Verdict Formatter(RepoContext c) => Verdict.When(
        c.Has(".editorconfig") && c.Contains(Agents, "dotnet format"),
        ".editorconfig present; AGENTS.md lists dotnet format",
        c.Has(".editorconfig") ? "AGENTS.md doesn't list dotnet format" : "no .editorconfig at the root");

    private static Verdict PreCommit(RepoContext c)
    {
        var config = c.FirstExisting(".pre-commit-config.yaml", ".githooks", "lefthook.yml", ".husky");
        return Verdict.When(config is not null, $"tracked hook config: {config}",
            "no tracked hook config (.pre-commit-config.yaml, .githooks/, lefthook.yml, .husky/); hooks installed per clone are not reproducible");
    }

    private static Verdict Documented(RepoContext c, string command) => Verdict.When(
        c.Contains(Agents, command), $"AGENTS.md lists '{command}'", $"AGENTS.md doesn't list '{command}'");

    private static Verdict Pinned(RepoContext c)
    {
        var central = c.Contains(Packages, "<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>");
        var locks = c.Contains(BuildProps, "<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>");
        var all = c.Tree.Files("src", ".csproj").Concat(c.Tree.Files("tests", ".csproj")).ToList();
        var optedOut = all.Where(p => c.Contains(p, "<RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>")).ToList();
        var projects = all.Except(optedOut, StringComparer.Ordinal).ToList();
        var unlocked = projects.Where(p => !c.Has(p[..(p.LastIndexOf('/') + 1)] + "packages.lock.json")).ToList();
        var exempt = optedOut.Count == 0 ? "" : $"; opted out: {string.Join(", ", optedOut)}";
        return !central || !locks
            ? Verdict.Fail("Directory.Packages.props or Directory.Build.props doesn't turn on central versions and lock files")
            : Verdict.When(unlocked.Count == 0,
                string.Create(CultureInfo.InvariantCulture, $"central versions, lock files on, {projects.Count} projects locked{exempt}"),
                string.Create(CultureInfo.InvariantCulture, $"{unlocked.Count} of {projects.Count} projects have no packages.lock.json: {string.Join(", ", unlocked.Take(5))}{exempt}"));
    }

    private static Verdict VcsCli(RepoContext c)
    {
        var doc = new[] { "README.md", Agents }.FirstOrDefault(p => c.Tree.ReadText(p) is { } t && GhWord.IsMatch(t));
        return Verdict.When(doc is not null, $"{doc} documents the gh CLI", "neither README.md nor AGENTS.md names the gh CLI");
    }

    private static Verdict Releases(RepoContext c)
    {
        if (c.Host.Releases.PublishedAt is not { } published)
        {
            return Verdict.Unknown(c.Host.Releases.Evidence);
        }

        var recent = published.Count(p => p > c.Now.AddDays(-90) && p <= c.Now);
        var noun = recent == 1 ? "release" : "releases";
        var measured = string.Create(CultureInfo.InvariantCulture, $"{recent} {noun} in the 90 days before {c.Now:yyyy-MM-dd}");
        return Verdict.When(recent >= 3, measured, measured + " (needs 3)");
    }

    private static Verdict RuntimePinned(RepoContext c)
    {
        var text = c.Tree.ReadText("global.json");
        return text is null
            ? Verdict.Fail("no global.json")
            : Verdict.When(text.Contains("\"version\"", StringComparison.Ordinal) && RollForwardDisable.IsMatch(text),
                "global.json pins the SDK version with rollForward disable",
                "global.json doesn't pin an exact SDK (needs \"version\" and \"rollForward\": \"disable\")");
    }

    [GeneratedRegex(@"(?<![A-Za-z0-9_-])gh(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GhWord { get; }

    [GeneratedRegex(@"""rollForward""\s*:\s*""disable""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RollForwardDisable { get; }
}
