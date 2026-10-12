namespace Aw.Context.Core.Engine;

/// <summary>A repo as the profile writes it, before the engine checks its name (<see cref="ScopeRules.CheckRepos"/>).</summary>
/// <param name="Name">The repo's name as written.</param>
/// <param name="Path">The absolute path of its checkout.</param>
public sealed record ProfileRepoEntry(string Name, string Path);
