
namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The open run's inputs, in declaration order (plan "How the text reaches the Step without being stored").</summary>
/// <param name="State">The state whose Step runs.</param>
/// <param name="Run">The run token.</param>
/// <param name="Inputs">The inputs the run filled.</param>
public sealed record InjectionSource(string State, string Run, IReadOnlyList<InjectionInputSource> Inputs);
