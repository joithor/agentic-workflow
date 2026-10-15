namespace Aw.Context.Core.Engine;

/// <summary>'context.labeling' (spec §8.8): the optional model pass that names clusters. Off until the profile turns it on.</summary>
public sealed record LabelingSettings
{
    /// <summary>Whether a build may send file paths to a model to name clusters.</summary>
    public bool Enabled { get; init; }

    /// <summary>The most tokens one build's labeling pass may spend.</summary>
    public int MaxTokens { get; init; } = 400_000;
}
