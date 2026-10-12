namespace Aw.Context.Core.Engine;

/// <summary>A repo the profile names.</summary>
/// <param name="Name">Its short name (the 'repo:' key prefix and the X-Sindri-Repo scope).</param>
/// <param name="Path">The absolute path of its checkout.</param>
public sealed record ContextRepo(string Name, string Path);
