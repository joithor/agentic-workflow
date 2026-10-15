using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Make the latest draft the current map (context spec §8.7), a protected command (README X30, X63): without an answer it shows the
/// draft and issues the challenge 'promote map v&lt;N&gt;'; with one, <see cref="Answer"/> must redeem it. A draft with undecided
/// review items, or none, is SND-CONTEXT-046.
/// </summary>
/// <param name="Draft">The draft the human confirmed ('v14@0a1b2c3'), or null to see it and get a challenge.</param>
/// <param name="Answer">The challenge and the text the human typed, or null.</param>
public sealed record PromoteContextMapRequest(string? Draft = null, ChallengeAnswer? Answer = null) : ICommand<PromoteContextMapResponse>;
