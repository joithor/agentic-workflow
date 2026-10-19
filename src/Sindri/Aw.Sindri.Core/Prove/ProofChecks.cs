using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Prove;

/// <summary>The proof pack's rules (line spec §7.3, PRV-*): pure decisions over <see cref="ProofFacts"/>.</summary>
public static class ProofChecks
{
    /// <summary>Runs every rule.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>The failures and the rules that did not run.</returns>
    public static ProofReport Run(ProofFacts facts) => throw new NotSupportedException("red: " + facts?.Head);

    /// <summary>The ids of the phase's items that still need proving: every item that is not superseded.</summary>
    /// <param name="acceptance">The phase packet's acceptance.json.</param>
    /// <returns>The ids.</returns>
    public static IReadOnlyList<string> LiveItems(JsonNode acceptance) => throw new NotSupportedException("red: " + acceptance?.ToString());
}
