using Aw.Kernel;

namespace Aw.Readiness.Core;

/// <summary>A repo's readiness.yaml: who it is, its ring, the scope it's checked as, and each ring's target level.</summary>
/// <param name="Repo">The name its history is kept under.</param>
/// <param name="Ring">Its ring.</param>
/// <param name="Scope">What is checked (today only 'csharp-template').</param>
/// <param name="Targets">The target level per ring.</param>
public sealed record ReadinessConfig(RepoName Repo, int Ring, string Scope, IReadOnlyList<ReadinessTarget> Targets)
{
    /// <summary>The file, at the repo root.</summary>
    public const string FileName = "readiness.yaml";

    /// <summary>The scopes this version checks.</summary>
    public const string CSharpTemplateScope = "csharp-template";

    /// <summary>The target for this repo's ring.</summary>
    public ReadinessTarget Target => Targets.First(t => t.Ring == Ring);

    /// <summary>Parses readiness.yaml (a flat YAML subset: 'key: value' lines and one 'targets:' list of
    /// '- ring: N' / 'level: N' items; # comments).</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The config, or AW-READINESS-003 naming the line and the fix.</returns>
    public static Result<ReadinessConfig> Parse(string text) => ReadinessConfigParser.Parse(text);
}
