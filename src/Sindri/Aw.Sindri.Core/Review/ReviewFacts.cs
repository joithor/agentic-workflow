using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Review;

/// <summary>What the review loop knows when it records its result (line spec §3.2 row 6).</summary>
/// <param name="Head">The reviewed head sha ('fact:change').</param>
/// <param name="Review">The running review ('fact:review': round, verdict, counts and the last round's findings).</param>
/// <param name="LensRounds">The lens log ('fact:lensRounds'): one entry per lens run, each with its round; null when the fact holds no array.</param>
public sealed record ReviewFacts(string Head, JsonNode Review, JsonNode? LensRounds);
