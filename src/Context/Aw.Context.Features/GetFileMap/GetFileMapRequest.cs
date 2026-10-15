using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;

namespace Aw.Context.Features.GetFileMap;

/// <summary>The direct children of a repo path with their status.</summary>
internal sealed record GetFileMapRequest(FileMapQuery Query, ContextScope Scope);
