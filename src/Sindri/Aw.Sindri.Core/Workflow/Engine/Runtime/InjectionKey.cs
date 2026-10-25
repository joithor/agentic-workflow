namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Where one Step input entry's texts are kept: the instance, the Step's state, the run (attempt) and the entry, which is its
/// 1-based place in the Step's 'inputs:' list. The place, not the block id: the same block listed twice (with different 'with:')
/// is two entries with two texts.
/// </summary>
/// <param name="Address">The instance address.</param>
/// <param name="State">The state whose Step runs.</param>
/// <param name="Run">The run token.</param>
/// <param name="Entry">The entry's 1-based place in the Step's input list.</param>
public readonly record struct InjectionKey(string Address, string State, string Run, int Entry);
