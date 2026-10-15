using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>What the review loop does next.</summary>
/// <param name="Prompt">The text to show, or null when the loop is over.</param>
/// <param name="Send">A review request to send now (then show <paramref name="Prompt"/>), or null.</param>
internal sealed record ReviewStep(string? Prompt, ReviewContextMapRequest? Send = null);
