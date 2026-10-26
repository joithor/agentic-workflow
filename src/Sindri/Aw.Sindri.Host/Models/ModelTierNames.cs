using Aw.Judge.Core.Providers;

namespace Aw.Sindri.Host.Models;

/// <summary>The model each agent CLI runs for the strong tier. The cheap tier names none: each CLI keeps its own small default.</summary>
/// <param name="Claude">`claude --model`.</param>
/// <param name="Codex">`codex exec --model`.</param>
/// <param name="Cursor">`cursor-agent --model`.</param>
internal sealed record ModelTierNames(string Claude, string Codex, string Cursor)
{
    /// <summary>The defaults.</summary>
    public static ModelTierNames Default { get; } = new("sonnet", "gpt-6", "gpt-5.6");

    /// <summary>The strong model for an agent CLI's provider, or null for a provider that is not one.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The model name.</returns>
    public string? Strong(ProviderName provider) => provider == ProviderName.Rules ? Claude : null;
}
