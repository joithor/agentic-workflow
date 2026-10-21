using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.Scoping;

/// <summary>One rule's status in a run, with the fact keys it was missing when it did not run.</summary>
/// <param name="Rule">The rule id.</param>
/// <param name="Status">Its status.</param>
/// <param name="Missing">The fact keys (no 'fact:' prefix) a not-run rule needed; empty otherwise.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScopeRuleResult(string Rule, ScopeRuleStatus Status, IReadOnlyList<string> Missing);
