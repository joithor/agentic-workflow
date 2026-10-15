namespace Aw.Sindri.Core.Challenges;

/// <summary>A protected request's answer to its challenge (engine spec §4.2): the challenge id and the text the human typed at the TTY.</summary>
/// <param name="Challenge">The challenge's id.</param>
/// <param name="Typed">What the human typed.</param>
public sealed record ChallengeAnswer(ChallengeId Challenge, string Typed);
