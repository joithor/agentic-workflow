using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>Line 1 of an eval case (spec §9.2): '{ "start": { "inputs": { … } } }', with '"scaffold": true' when '--scaffold' generated it.</summary>
/// <param name="Start">The start.</param>
/// <param name="Scaffold">True on a generated happy-path case; the approve preview marks it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EvalStartLine(EvalStart Start, bool? Scaffold);
