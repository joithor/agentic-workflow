namespace Aw.Meta.Core.Config;

/// <summary>What the gate runs for a pull request, chosen by the paths it changes.</summary>
/// <param name="Always">Scopes that run for every pull request.</param>
/// <param name="DocsOnly">Globs; when every changed path matches one, only the always scopes run.</param>
/// <param name="Scopes">Every scope by name; the order is the order they run in.</param>
/// <param name="ParallelSteps">How many heavy steps of one gate run at once; absent: the team's heavy cap (<see cref="HeavyConfig.Cap"/>). 1 runs the gate serially.</param>
public sealed record GateConfig(IReadOnlyList<string> Always, IReadOnlyList<string> DocsOnly, IReadOnlyDictionary<string, ScopeConfig> Scopes, int? ParallelSteps = null)
{
    /// <summary>A gate with no scopes.</summary>
    public static GateConfig Empty { get; } = new([], [], new Dictionary<string, ScopeConfig>(StringComparer.Ordinal));
}
