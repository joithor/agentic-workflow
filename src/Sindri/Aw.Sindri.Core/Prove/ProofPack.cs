using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Prove;

/// <summary>The proof pack (line spec §3.2 row 5, §7.3): the files <c language="text">step.prove.evidence</c> writes and machine-freezes, as pure functions of <see cref="ProofFacts"/>.</summary>
public static class ProofPack
{
    /// <summary>The names of the pack's files.</summary>
    public static IReadOnlyList<string> FileNames { get; } = ["packet.json", "index.json", "proof.json", "red.json", "before-after.json", "holdout.json", "not-proven.md", "acceptance.json"];

    /// <summary>The acceptance file the pack carries.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>The file's JSON.</returns>
    public static JsonNode Acceptance(ProofFacts facts) => throw new NotSupportedException("red: " + facts?.Head);

    /// <summary>PRV-ACC-002: the pack's acceptance only flips statuses, each flip with evidence.</summary>
    /// <param name="before">The phase's acceptance file.</param>
    /// <param name="after">The pack's.</param>
    /// <returns>The findings.</returns>
    public static IReadOnlyList<ProofFinding> AcceptanceFlips(JsonNode before, JsonNode after) => throw new NotSupportedException("red: " + before?.ToString() + after?.ToString());

    /// <summary>Every file of the pack as text, by name.</summary>
    /// <param name="facts">The facts.</param>
    /// <param name="report">What the rules found.</param>
    /// <param name="origin">The phase packet the pack proves.</param>
    /// <returns>File name to text.</returns>
    public static IReadOnlyDictionary<string, string> Files(ProofFacts facts, ProofReport report, ProofPackOrigin origin) => throw new NotSupportedException("red: " + facts?.Head + report?.Holds + origin?.Id);

    /// <summary>'sha256:' and the hex digest of a text's UTF-8 bytes.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The hash.</returns>
    public static string Sha256(string text) => throw new NotSupportedException("red: " + text);
}
