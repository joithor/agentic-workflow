using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Steps;

namespace Aw.Evidence.Core.Runs;

/// <summary>One executed step in one viewport.</summary>
/// <param name="Viewport">The viewport.</param>
/// <param name="Index">The step's 1-based position in the script.</param>
/// <param name="Action">The action.</param>
/// <param name="Target">The action's target.</param>
/// <param name="Status">How it ended.</param>
/// <param name="Screenshot">The screenshot taken after it (absolute path).</param>
public sealed record RunStep(ViewportKind Viewport, int Index, StepAction Action, string Target, StepStatus Status, string Screenshot);
