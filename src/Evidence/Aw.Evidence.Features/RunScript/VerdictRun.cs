using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Scripts;

namespace Aw.Evidence.Features.RunScript;

/// <summary>The driven run the visual verdict is decided for.</summary>
/// <param name="Request">The run's request (baseline, app build, fixtures, cache path, run directory).</param>
/// <param name="Instance">This invocation (the verdict cache trusts an entry without an app build only within it).</param>
/// <param name="Script">The script that ran.</param>
/// <param name="Driven">What driving it produced.</param>
internal sealed record VerdictRun(RunScriptRequest Request, RunInstanceId Instance, UiScript Script, DrivenRun Driven);
