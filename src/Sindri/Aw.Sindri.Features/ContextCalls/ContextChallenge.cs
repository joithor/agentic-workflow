using Aw.Sindri.Core.Challenges;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The challenge a protected context command's preview issued: the human types <paramref name="Action"/> back at a terminal before it expires (README X30).</summary>
/// <param name="Challenge">Its id, sent back with the typed text.</param>
/// <param name="Action">The action text to type, for example 'promote map v14'.</param>
/// <param name="ExpiresAt">When it stops being accepted.</param>
public sealed record ContextChallenge(ChallengeId Challenge, string Action, DateTimeOffset ExpiresAt);
