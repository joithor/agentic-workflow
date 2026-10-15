namespace Aw.Meta.Core.Config;

/// <summary>A gate scope: the steps to run when a changed path matches one of its globs.</summary>
/// <param name="Paths">Globs over repository-relative paths.</param>
/// <param name="Steps">The steps, in order.</param>
public sealed record ScopeConfig(IReadOnlyList<string> Paths, IReadOnlyList<StepConfig> Steps);
