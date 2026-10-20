namespace Aw.Sindri.Core.Challenges;

/// <summary>
/// A protected request's answer to its challenge (engine spec §4.2; setup-once spec §4 item 3): the challenge id and either the text the human typed at the TTY or,
/// for a held challenge, how long the app's button was held.
/// </summary>
/// <param name="Challenge">The challenge's id.</param>
/// <param name="Typed">What the human typed; empty for a held answer.</param>
/// <param name="HeldMs">Milliseconds the button was held, or null for a typed answer.</param>
public sealed record ChallengeAnswer(ChallengeId Challenge, string Typed = "", long? HeldMs = null);
