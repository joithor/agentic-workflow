using Aw.Sindri.Core.Challenges;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The challenge an import preview issued: the human types <paramref name="Action"/> back at a terminal before it expires.</summary>
/// <param name="Challenge">Its id, sent back with the typed text.</param>
/// <param name="Action">The action text to type: 'import map &lt;hash6&gt;'.</param>
/// <param name="ExpiresAt">When it stops being accepted.</param>
public sealed record MapImportChallenge(ChallengeId Challenge, string Action, DateTimeOffset ExpiresAt);
