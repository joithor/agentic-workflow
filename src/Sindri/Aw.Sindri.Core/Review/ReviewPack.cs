using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Review;

/// <summary>
/// The review result packet (line spec §3.2 row 6, §7.4): the files <c language="text">step.review.record</c> writes into the run area and machine-freezes, as pure functions of <see cref="ReviewFacts"/>.
/// </summary>
public static class ReviewPack
{
    /// <summary>The names of the packet's files, in the order the packet's links list them.</summary>
    public static IReadOnlyList<string> FileNames { get; } = ["packet.json", "index.json", "review.json"];

    /// <summary>The verdict a result can be recorded under: 'clean' or 'behaviourWrong'.</summary>
    /// <param name="review">The running review.</param>
    /// <returns>The verdict; null when the review holds none a result can be recorded under.</returns>
    public static string? Verdict(JsonNode review)
    {
        ArgumentNullException.ThrowIfNull(review);
        throw new NotSupportedException();
    }

    /// <summary>Why the review cannot be recorded: a verdict nothing can be recorded under, or a clean verdict with blocking or issue findings left.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>One text per problem; empty when the result can be recorded.</returns>
    public static IReadOnlyList<string> Problems(ReviewFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        throw new NotSupportedException();
    }

    /// <summary>Every file of the packet as text, by name.</summary>
    /// <param name="facts">The facts.</param>
    /// <param name="origin">The phase packet the result reviews.</param>
    /// <returns>File name to text.</returns>
    public static IReadOnlyDictionary<string, string> Files(ReviewFacts facts, ReviewPackOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(origin);
        throw new NotSupportedException();
    }
}
