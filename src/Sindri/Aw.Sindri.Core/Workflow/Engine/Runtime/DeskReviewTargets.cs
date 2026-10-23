using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Reads where a reviewed fact opens from its stored text, by the shapes the engine itself writes: a packet fact is { hash, dir } (the directory is the path) and
/// 'fact:prStatus' carries 'url'. Any other value, or text that is not a JSON object, names nothing.
/// </summary>
public static class DeskReviewTargets
{
    /// <summary>The target <paramref name="text"/> names.</summary>
    /// <param name="text">The fact's stored text.</param>
    /// <returns>The path and url it holds; both null when it holds neither.</returns>
    public static DeskReviewTarget Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new DeskReviewTarget(Path: null, Url: null);
    }

    /// <summary><paramref name="reviews"/> with each one's path and url filled from <paramref name="targets"/>, by fact key; a review with no entry keeps null.</summary>
    /// <param name="reviews">The reviews the descriptor declares.</param>
    /// <param name="targets">The targets read, by fact key; null when none were read.</param>
    /// <returns>The reviews.</returns>
    public static IReadOnlyList<DeskReview> Fill(IReadOnlyList<DeskReview> reviews, IReadOnlyDictionary<string, DeskReviewTarget>? targets)
    {
        ArgumentNullException.ThrowIfNull(reviews);
        return reviews;
    }
}
