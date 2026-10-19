using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.Scoping;

/// <summary>One finding, the same shape everywhere the checks run (scoping spec §8.2).</summary>
/// <param name="Rule">The rule id, for example 'SCP-CITE-001'.</param>
/// <param name="Item">What it is about (an id, a key, a lens), or null for the whole fact.</param>
/// <param name="Message">What is wrong.</param>
/// <param name="Fix">What to change.</param>
/// <param name="Severity">Fail or warn.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScopeFinding(string Rule, string? Item, string Message, string Fix, ScopeSeverity Severity);
