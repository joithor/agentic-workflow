using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.StartInstance;

/// <summary>What a start stored: the input facts, and the instant the start is stamped with (spec section 3.11).</summary>
/// <param name="Inputs">The stored input facts, the generated ones included.</param>
/// <param name="StartedAt">The start time.</param>
/// <param name="StartedBy">Who started it (the caller's actor); null when the start runs outside any call.</param>
internal sealed record StartedFacts(IReadOnlyDictionary<FactKey, InputFact> Inputs, DateTimeOffset StartedAt, string? StartedBy = null);
