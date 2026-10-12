using Aw.Context.Core.Engine;

namespace Aw.Context.Features.ImportMap;

/// <summary>Imports a map file as the current version, bound to the content hash the owner confirmed.</summary>
/// <param name="Path">The map file.</param>
/// <param name="ConfirmedHash">The hash the owner confirmed.</param>
/// <param name="Scope">The profile's context section and repos.</param>
internal sealed record ImportMapRequest(string Path, string ConfirmedHash, ContextScope Scope);
