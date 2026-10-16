using Aw.Context.Core.Engine;

namespace Aw.Context.Features.PromoteMap;

/// <summary>Makes the latest draft current; refused while review items are undecided.</summary>
internal sealed record PromoteMapRequest(ContextScope Scope);
