namespace Aw.Meta.Core.Config;

/// <summary>One step of a gate scope: a command run in the gate tree.</summary>
/// <param name="Name">The step name, unique within its scope.</param>
/// <param name="Run">The command line as an argument vector; ["bash","-c","..."] for a shell line.</param>
/// <param name="Heavy">Whether the step runs under the heavy-job wrapper when one is installed.</param>
public sealed record StepConfig(string Name, IReadOnlyList<string> Run, bool Heavy = false);
