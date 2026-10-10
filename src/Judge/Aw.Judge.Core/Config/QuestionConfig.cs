using System.Text.Json.Serialization;

namespace Aw.Judge.Core.Config;

/// <summary>One question's settings in config.json.</summary>
/// <param name="Enabled">Whether the question asks a provider at all.</param>
/// <param name="Threshold">The confidence a provider's answer needs.</param>
public sealed record QuestionConfig(bool Enabled, double Threshold)
{
    /// <summary>The providers this question may use, in order (e.g. ["jev", "rules"]); null for the default chain.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Providers { get; init; }
}
