using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Questions;

/// <summary>
/// visual-critique: does a screenshot look right ('looks-right'), have specific problems ('looks-off') or look careless
/// ('sloppy'), with 'reasons'? No rule: whether it looks right is exactly the judgment this question exists for.
/// </summary>
public static class VisualCritiqueQuestion
{
    /// <summary>The question's name.</summary>
    public const string Name = "visual-critique";

    /// <summary>It looks right.</summary>
    public const string LooksRight = "looks-right";

    /// <summary>It has specific visual problems.</summary>
    public const string LooksOff = "looks-off";

    /// <summary>It looks careless throughout.</summary>
    public const string Sloppy = "sloppy";

    /// <summary>The definition.</summary>
    public static QuestionDefinition<VisualCritiqueInput> Definition { get; } = new()
    {
        Name = Name,
        Outputs = [LooksRight, LooksOff, Sloppy],
        Criteria = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LooksRight] = "alignment, spacing, hierarchy and weight look right and match the reference",
            [LooksOff] = "there are specific visual problems compared with the reference or expectations",
            [Sloppy] = "the page looks careless, with pervasive alignment, spacing or hierarchy problems",
        },
        ContentClass = ContentClass.Image,
        Threshold = 0.5,
        TimeBudget = TimeSpan.FromSeconds(20),
        ExtraProperties = new Dictionary<string, JsonObject>(StringComparer.Ordinal) { [ResolutionCheckQuestion.Reasons] = ResolutionCheckQuestion.StringList() },
        Prompt = Prompt,
    };

    // Provider-neutral: a CLI provider reads the file from the evidence directory or attaches it as an image.
    private static string Prompt(VisualCritiqueInput input) => string.Join('\n',
    [
        $"Look at the screenshot {input.AfterScreenshot}.",
        input.BaselineScreenshot is { } baseline
            ? $"Then look at {baseline}, a screenshot of the same page on the base branch before this change, and compare them."
            : "There is no base-branch screenshot to compare against: judge it on its own.",
        "Rate alignment, spacing, typographic hierarchy, visual weight, and consistency with the reference (when given).",
        "Reply {\"decision\":\"looks-right\",\"reasons\":[]} if it looks right, or {\"decision\":\"looks-off\",\"reasons\":[\"...\"]} or {\"decision\":\"sloppy\",\"reasons\":[\"...\"]} listing the specific problems. Never guess \"looks-right\" if anything looks wrong.",
    ]);
}
