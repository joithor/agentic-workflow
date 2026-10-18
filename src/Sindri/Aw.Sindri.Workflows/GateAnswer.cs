using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One gate's answer (engine P7 plan D7). A gate with no answer had no runtime, or its runtime threw; the decision refuses it as unavailable (W5).</summary>
/// <param name="Gate">The gate.</param>
/// <param name="Result">Its verdict, score and reasons.</param>
internal sealed record GateAnswer(BlockId Gate, GateResult Result);
