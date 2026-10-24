using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Prove;

/// <summary>
/// What <see cref="ProofChecks"/> reads (line spec §7.3): the phase's own files and the facts the prove station gathered. A fact the instance did not carry is null, and a rule that needs it
/// is reported as not run, never as held.
/// </summary>
/// <param name="Head">The head sha every artifact must name (fact:built's head).</param>
/// <param name="Acceptance">The phase packet's acceptance.json.</param>
/// <param name="Signals">The phase packet's signals.json as the line writes it (<see cref="PhaseSignals.Shape"/>), or null; <see cref="PhaseSignals.Of"/> reads it.</param>
/// <param name="PhaseKind">phase.json's kind: feature, fix, refactor, docs or config; empty when unknown.</param>
/// <param name="HoldoutCount">The hidden set's size from the phase packet's holdout.json, without the canary scenario; null when unknown.</param>
/// <param name="Proof">fact:proof: <c language="text">{head, items[{id, signals[], artifacts[{kind, path, sha256, commit, at, passing, signal?, superseded?}]}]}</c>, or null.</param>
/// <param name="Red">fact:redTests (spec A.7, §7.2): an object keyed by item id, each value a list of test ids (<c language="text">["T1"]</c>) or <c language="text">{tests?: [id | {id, path}], appOnly?: {signal}, commit?, output?}</c>; <c language="text">{}</c> before any red test was recorded.</param>
/// <param name="BeforeAfter">fact:beforeAfter: <c language="text">{commit, results[{item, signal, base, head, baseScript?, headScript?}]}</c>.</param>
/// <param name="Holdout">fact:holdoutResult: <c language="text">{commit, canary?, results[{scenario, item, verdict, spent?, kind?}]}</c>; the canary's result (<c language="text">kind</c> 'canary', or no <c language="text">item</c>) is not a hidden scenario's.</param>
/// <param name="HoldoutMin">workflow.prove.holdoutMin: the hidden scenarios that must never have been spent.</param>
public sealed record ProofFacts(
    string Head,
    JsonNode Acceptance,
    JsonNode? Signals,
    string PhaseKind,
    int? HoldoutCount,
    JsonNode? Proof,
    JsonNode? Red,
    JsonNode? BeforeAfter,
    JsonNode? Holdout,
    int HoldoutMin = ProofFacts.DefaultHoldoutMin)
{
    /// <summary>The default for workflow.prove.holdoutMin (line spec Appendix D).</summary>
    public const int DefaultHoldoutMin = 2;
}
