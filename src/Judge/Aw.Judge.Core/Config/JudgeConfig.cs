using System.Globalization;
using System.Text.Json.Serialization;
using Aw.Judge.Core.Errors;
using Aw.Kernel;

namespace Aw.Judge.Core.Config;

/// <summary>The judge's config.json: per-question settings, the provider selection and the prompt sorter's overrides.</summary>
/// <param name="Questions">The questions' settings by name; always holds wake-gate.</param>
/// <param name="Providers">The provider selection, when the file sets one.</param>
/// <param name="PromptSort">The prompt sorter's overrides, when the file sets them.</param>
public sealed record JudgeConfig(
    IReadOnlyDictionary<string, QuestionConfig> Questions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProvidersConfig? Providers,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PromptSortConfig? PromptSort)
{
    /// <summary>A question's settings when config.json doesn't name it.</summary>
    public static QuestionConfig DefaultQuestion { get; } = new(Enabled: true, Threshold: 0.7);

    /// <summary>The config with no file: wake-gate enabled at 0.7.</summary>
    public static JudgeConfig Default { get; } = new(
        new Dictionary<string, QuestionConfig>(StringComparer.Ordinal) { ["wake-gate"] = DefaultQuestion }, Providers: null, PromptSort: null);

    /// <summary>'judge config set': sets one question's enabled flag ("true" is true, anything else false) or, for any
    /// other field, its threshold; the providers and promptSort blocks are kept.</summary>
    /// <param name="question">The question name.</param>
    /// <param name="field">"enabled", or the threshold for anything else (as the TS judge reads it).</param>
    /// <param name="value">The value as typed.</param>
    /// <returns>The new config, or AW-JUDGE-006 when a threshold isn't a finite number.</returns>
    public Result<JudgeConfig> Set(string question, string field, string value)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(value);
        var existing = Questions.TryGetValue(question, out var current) ? current : DefaultQuestion;
        QuestionConfig updated;
        if (string.Equals(field, "enabled", StringComparison.Ordinal))
        {
            updated = existing with { Enabled = string.Equals(value, "true", StringComparison.Ordinal) };
        }
        else if (double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold) && double.IsFinite(threshold))
        {
            updated = existing with { Threshold = threshold };
        }
        else
        {
            return JudgeErrors.NotANumber.With($"not a number: {value}");
        }

        return this with { Questions = new Dictionary<string, QuestionConfig>(Questions, StringComparer.Ordinal) { [question] = updated } };
    }
}
