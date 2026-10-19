using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>One input of the open run: what the run recorded of it and what it fills from, to run it again when its text is not in memory.</summary>
/// <param name="Recorded">The metadata the 'FillInput' activity returned.</param>
/// <param name="Context">What the input fills from.</param>
public sealed record InjectionInputSource(FilledInput Recorded, StepInputContext Context);
