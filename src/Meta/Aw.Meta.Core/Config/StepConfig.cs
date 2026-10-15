namespace Aw.Meta.Core.Config;

/// <summary>One gate step.</summary>
/// <param name="Name">The step's name, unique within the gate; also its log's name.</param>
/// <param name="Run">The argument vector, run in the gate tree.</param>
/// <param name="Heavy">Whether it runs through the heavy-job wrapper.</param>
/// <param name="Rerun">How to run one test of this step: an argument vector where {test} stands for the test filter. Absent: a dotnet test step gets --filter-method *{test}* appended.</param>
public sealed record StepConfig(string Name, IReadOnlyList<string> Run, bool Heavy = false, IReadOnlyList<string>? Rerun = null);
