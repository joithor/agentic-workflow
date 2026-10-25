using Aw.Context.Core.Engine;

namespace Aw.Context.Features.PromoteMap;

/// <summary>Makes the latest draft current; refused while review items are undecided.</summary>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
/// <param name="ExpectedVersion">The draft the caller means, or null for whichever is latest.</param>
internal sealed record PromoteMapRequest(ContextScope Scope, int? ExpectedVersion = null);
