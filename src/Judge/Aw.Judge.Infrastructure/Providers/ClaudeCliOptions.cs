namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// The model `claude -p` runs with. The defaults are the chain's provider: haiku, low effort, thinking off (which is why
/// MAX_THINKING_TOKENS is 0 and alwaysThinkingEnabled false: ~2.7 s instead of ~15 s). The adjudicator asks opus with high effort.
/// </summary>
/// <param name="Model">The --model value.</param>
/// <param name="Effort">The --effort value.</param>
/// <param name="Thinking">Whether the model may think: false keeps MAX_THINKING_TOKENS=0 and alwaysThinkingEnabled false.</param>
internal sealed record ClaudeCliOptions(string Model = "haiku", string Effort = "low", bool Thinking = false)
{
    /// <summary>What the chain's claude-cli provider runs with.</summary>
    internal static ClaudeCliOptions Default { get; } = new();
}
