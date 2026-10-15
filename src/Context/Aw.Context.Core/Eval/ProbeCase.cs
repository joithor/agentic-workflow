using System.Text.Json.Serialization;

namespace Aw.Context.Core.Eval;

/// <summary>One probe: a question as a person would ask it, and what a right answer names.</summary>
/// <param name="Id">A stable slug.</param>
/// <param name="Question">The question.</param>
/// <param name="Expect">The expected anchors.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProbeCase(ProbeId Id, string Question, ProbeExpect Expect);
