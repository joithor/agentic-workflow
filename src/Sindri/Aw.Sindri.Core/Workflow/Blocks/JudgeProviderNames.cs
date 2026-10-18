namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>The provider names a Judge Step may list. Lives under Workflow so recipe validation can check a literal list without depending on the profile (which depends on Workflow).</summary>
public static class JudgeProviderNames
{
    /// <summary>The deterministic fallback provider, never dropped.</summary>
    public const string Rules = "rules";

    /// <summary>The opt-in provider.</summary>
    public const string Jev = "jev";

    /// <summary>Every provider name a profile or recipe may list.</summary>
    public static IReadOnlyList<string> Known { get; } = [Jev, "claude-cli", "codex-cli", "cursor-cli", Rules];
}
