using Aw.Sindri.Core.Challenges;

namespace Aw.Sindri.Features.LineHoldout;

/// <summary>A hidden scenario as the owner reads it.</summary>
/// <param name="Id">The scenario's id.</param>
/// <param name="Item">The visible acceptance item it is tied to.</param>
/// <param name="Kind">What runs it: api, ui, db and so on.</param>
/// <param name="Steps">What the evaluator does.</param>
/// <param name="Expected">What should happen.</param>
/// <param name="WasSpent">Whether it was already spent before this read.</param>
internal sealed record ShownScenario(string Id, string? Item, string Kind, IReadOnlyList<string> Steps, string Expected, bool WasSpent);

/// <summary>The challenge a protected show issued: the human types <paramref name="Action"/> back at a terminal before it expires.</summary>
/// <param name="Challenge">Its id, sent back with the typed text.</param>
/// <param name="Action">The text to type.</param>
/// <param name="ExpiresAt">When it stops being accepted.</param>
internal sealed record HoldoutChallenge(ChallengeId Challenge, string Action, DateTimeOffset ExpiresAt);

/// <summary>What a show answered.</summary>
/// <param name="Line">The line's slug.</param>
/// <param name="Phase">The phase key.</param>
/// <param name="SetHash">The hash of the set file.</param>
/// <param name="Total">The hidden scenarios, the canary not counted.</param>
/// <param name="Spent">How many of them are spent already.</param>
/// <param name="Scenarios">The scenarios with their text after the confirmation; null in the preview.</param>
/// <param name="Challenge">The preview's challenge; null after the confirmation.</param>
internal sealed record ShowLineHoldoutResponse(string Line, string Phase, string SetHash, int Total, int Spent, IReadOnlyList<ShownScenario>? Scenarios, HoldoutChallenge? Challenge);
