using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>A registered repo as the facade sees it: its name, its main checkout, and the index settings that apply to it (the profile's, with the repo's own entry merged in by Sindri, spec §13).</summary>
/// <param name="Name">Its short name.</param>
/// <param name="Path">The absolute path of its main checkout; read only, never written.</param>
/// <param name="Settings">The effective settings for this repo.</param>
public sealed record CodeIndexRepo(RepoName Name, string Path, CodeIndexSettings Settings);
