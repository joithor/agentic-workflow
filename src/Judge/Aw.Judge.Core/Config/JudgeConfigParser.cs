using System.Text.Json;

namespace Aw.Judge.Core.Config;

/// <summary>
/// Reads config.json leniently, as the TS judge's loadConfig did: a file that isn't a JSON object, or whose questions
/// block isn't an object, is the default config; unknown provider names and wrong-typed fields are dropped (a question
/// entry that isn't an object is dropped; a wrong-typed enabled or threshold gets its default), never fatal.
/// </summary>
public static class JudgeConfigParser
{
    private const double MinBudgetMs = 200;
    private const double MaxBudgetMs = 1050;   // the hook's 1500 ms kill minus the 450 ms startup allowance (TS config.ts)

    /// <summary>Parses config.json's text.</summary>
    /// <param name="text">The file's text, or null when there is no file.</param>
    /// <returns>The effective config.</returns>
    public static JudgeConfig Parse(string? text)
    {
        using var document = text is null ? null : Document(text);
        if (document?.RootElement is not { ValueKind: JsonValueKind.Object } root)
        {
            return JudgeConfig.Default;
        }

        var questions = root.TryGetProperty("questions", out var q) ? q : default;
        return questions.ValueKind is JsonValueKind.Undefined or JsonValueKind.Object
            ? new JudgeConfig(Questions(questions), Providers(root), PromptSort(root))
            : JudgeConfig.Default;
    }

    private static JsonDocument? Document(string text)
    {
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, QuestionConfig> Questions(JsonElement questions)
    {
        var parsed = new Dictionary<string, QuestionConfig>(JudgeConfig.Default.Questions, StringComparer.Ordinal);
        if (questions.ValueKind != JsonValueKind.Object)
        {
            return parsed;
        }

        foreach (var entry in questions.EnumerateObject().Where(static e => e.Value.ValueKind == JsonValueKind.Object))
        {
            var e = entry.Value;
            parsed[entry.Name] = new QuestionConfig(
                Boolean(e, "enabled") ?? JudgeConfig.DefaultQuestion.Enabled,
                Number(e, "threshold") ?? JudgeConfig.DefaultQuestion.Threshold)
            {
                Providers = Named(e, "providers", ProviderNames.All) is { Count: > 0 } named ? named : null,
            };
        }

        return parsed;
    }

    private static ProvidersConfig? Providers(JsonElement root)
    {
        if (!root.TryGetProperty("providers", out var block) || block.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var agentClis = Named(block, "agentClis", ProviderNames.AgentClis);
        var jev = Boolean(block, "jev");
        return agentClis is null && jev is null ? null : new ProvidersConfig(agentClis, jev);
    }

    private static PromptSortConfig? PromptSort(JsonElement root)
    {
        if (!root.TryGetProperty("promptSort", out var block) || block.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var budget = Number(block, "budgetMs");
        var cooldown = Number(block, "cooldownPrompts");
        return new PromptSortConfig(
            Boolean(block, "enabled"),
            budget is { } b ? Math.Clamp(b, MinBudgetMs, MaxBudgetMs) : null,
            cooldown >= 0 ? cooldown : null,
            Scaffolds(block));
    }

    private static Dictionary<string, bool>? Scaffolds(JsonElement block)
    {
        if (!block.TryGetProperty("scaffolds", out var scaffolds) || scaffolds.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var parsed = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var id in ProviderNames.Scaffolds)
        {
            if (Boolean(scaffolds, id) is { } on)
            {
                parsed[id] = on;
            }
        }

        return parsed;
    }

    // The array's strings that are known names, in order; null when the field isn't an array.
    private static List<string>? Named(JsonElement element, string name, IReadOnlyList<string> known) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(static v => v.ValueKind == JsonValueKind.String).Select(static v => v.GetString() ?? "").Where(known.Contains)]
            : null;

    private static bool? Boolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n) && double.IsFinite(n)
            ? n
            : null;
}
