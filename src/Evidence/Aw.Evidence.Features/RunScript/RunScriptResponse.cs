using Aw.Evidence.Core.Runs;

namespace Aw.Evidence.Features.RunScript;

/// <summary>The run's summary, as written to summary.json.</summary>
/// <param name="Summary">The summary.</param>
/// <param name="Passed">Whether every step passed.</param>
public sealed record RunScriptResponse(RunSummary Summary, bool Passed);
