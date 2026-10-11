using Aw.Evidence.Core.Runs;

namespace Aw.Evidence.Features.RunScript;

/// <summary>What driving the script in every viewport produced.</summary>
/// <param name="Steps">Every step, viewport by viewport, in order.</param>
/// <param name="LintFindings">The lint findings of each viewport's final page.</param>
/// <param name="Evidence">The traces and videos.</param>
/// <param name="BrowserVersion">The browser build.</param>
internal sealed record DrivenRun(IReadOnlyList<RunStep> Steps, IReadOnlyList<RunLintFinding> LintFindings, RunEvidence Evidence, string BrowserVersion);
