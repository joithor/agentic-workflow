using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>What one Step of the recipe reads and what leaving its state records (the run canvas's step cards).</summary>
/// <param name="State">The state that runs the Step.</param>
/// <param name="Needs">The fact keys the Step block reads (its descriptor's inputs), in declaration order.</param>
/// <param name="Produces">The facts the transitions out of the state record, each as the instance holds it so far (a fact not yet recorded carries only its key).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StateStep(string State, IReadOnlyList<string> Needs, IReadOnlyList<FactRef> Produces);
