using Aw.Context.Core.Lenses;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The direct children of a repo path with their map status (GET /sindri/context/filemap?repo=&amp;path=).</summary>
/// <param name="Repo">The repo's name in the profile.</param>
/// <param name="Path">The directory ('src/'), or null for the repo's root.</param>
/// <param name="Unmapped">True to keep only unmapped and suggested entries.</param>
/// <param name="Draft">True to read the draft map.</param>
public sealed record GetContextFileMapRequest(string Repo, string? Path = null, bool Unmapped = false, bool Draft = false) : IQuery<FileMapView>;
