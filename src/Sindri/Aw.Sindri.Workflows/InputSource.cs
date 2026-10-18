using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One input of a run: what the run recorded of it and what it fills from.</summary>
/// <param name="Recorded">The metadata the 'FillInput' activity returned.</param>
/// <param name="Context">What the input fills from, to run it again on a cache miss.</param>
internal sealed record InputSource(FilledInput Recorded, StepInputContext Context);
