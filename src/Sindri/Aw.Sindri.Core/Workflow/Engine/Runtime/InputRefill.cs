using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>One input of one run to get the text of again.</summary>
/// <param name="Address">The instance address.</param>
/// <param name="State">The state whose Step runs.</param>
/// <param name="Run">The run token.</param>
/// <param name="Input">The input.</param>
/// <param name="Mismatch">The error a differing refill answers.</param>
public sealed record InputRefill(string Address, string State, string Run, InjectionInputSource Input, ErrorDefinition Mismatch);
