namespace Aw.Context.Core.Engine;

/// <summary>'context.labeling' (spec §8.8): the optional model pass that names clusters. On unless the profile sets 'context.labeling.enabled' to false.</summary>
public sealed record LabelingSettings
{
    /// <summary>Whether a build may send file paths to a model to name clusters.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>The most tokens one build's labeling pass may spend.</summary>
    public int MaxTokens { get; init; } = 400_000;

    /// <summary>The most model tokens one sync may spend on decision extraction and conflict checks (spec §8.8, S-5B).</summary>
    public int MaxTokensPerSync { get; init; } = 50_000;
}
