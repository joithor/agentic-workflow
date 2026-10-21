using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.HoldMap;

/// <summary>Holds the latest draft for the code index.</summary>
/// <param name="Hold">The draft, and the time now as the start of the wait.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record HoldMapRequest(MapHold Hold, ContextScope Scope);
