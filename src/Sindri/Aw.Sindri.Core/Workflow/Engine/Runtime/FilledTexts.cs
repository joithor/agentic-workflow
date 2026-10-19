using System.Text.Json;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What one input filled, text and all: held only in memory (plan: no text is stored), scrubbed and capped.</summary>
/// <param name="Sections">The scrubbed text of each section by name.</param>
/// <param name="Result">The typed result (W6), scrubbed; null when the input returned none.</param>
public sealed record FilledTexts(IReadOnlyDictionary<string, string> Sections, JsonElement? Result = null);
