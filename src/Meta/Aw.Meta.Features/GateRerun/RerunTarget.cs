using Aw.Meta.Core.Gate;

namespace Aw.Meta.Features.GateRerun;

/// <summary>What a rerun is about: the gate run, its red step and the command that runs one test of it.</summary>
/// <param name="Gate">The latest gate run.</param>
/// <param name="Step">The step as recorded in that run.</param>
/// <param name="Command">The argument vector that runs one test.</param>
internal sealed record RerunTarget(GateRecord Gate, GateStep Step, IReadOnlyList<string> Command);
