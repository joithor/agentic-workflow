using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Where one input's texts are kept: the instance, the Step's state, the run (attempt) and the input.</summary>
/// <param name="Address">The instance address.</param>
/// <param name="State">The state whose Step runs.</param>
/// <param name="Run">The run token.</param>
/// <param name="Input">The input block.</param>
public readonly record struct InjectionKey(string Address, string State, string Run, BlockId Input);
