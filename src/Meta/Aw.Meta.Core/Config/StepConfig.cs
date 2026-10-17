namespace Aw.Meta.Core.Config;

/// <summary>One gate step.</summary>
/// <param name="Name">The step's name, unique within the gate; also its log's name.</param>
/// <param name="Run">The argument vector, run in the gate tree.</param>
/// <param name="Heavy">Whether it runs through the heavy-job wrapper.</param>
/// <param name="Rerun">How to run one test of this step: an argument vector where {test} stands for the test filter. Absent: a dotnet test step gets --filter-method *{test}* appended.</param>
/// <param name="After">The steps (of the same gate) this one waits for. Absent: inferred (dotnet build steps chain; a heavy or dotnet step waits for the builds; the rest run at once). An empty list: waits for nothing. A name that is configured but not selected for the run is ignored.</param>
/// <param name="TermGraceSeconds">How long the step gets to exit after SIGTERM (a cancelled gate) before it and what it started are killed. Absent: 45 seconds.</param>
public sealed record StepConfig(string Name, IReadOnlyList<string> Run, bool Heavy = false, IReadOnlyList<string>? Rerun = null, IReadOnlyList<string>? After = null, int? TermGraceSeconds = null);
