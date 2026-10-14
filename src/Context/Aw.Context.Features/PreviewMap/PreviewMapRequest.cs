using Aw.Context.Core.Engine;

namespace Aw.Context.Features.PreviewMap;

/// <summary>Reads and checks a map file without writing: what the owner confirms by its hash.</summary>
/// <param name="Path">The map file.</param>
/// <param name="Scope">The profile's context section and repos.</param>
internal sealed record PreviewMapRequest(string Path, ContextScope Scope);
