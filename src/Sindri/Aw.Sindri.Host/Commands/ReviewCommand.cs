using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>A parsed 'context map review' command line.</summary>
/// <param name="Mode">What it does.</param>
/// <param name="Json">True for --json.</param>
/// <param name="Request">The request to send for <see cref="ReviewMode.Request"/>, or null.</param>
/// <param name="Path">The export path for <see cref="ReviewMode.Export"/>, or null.</param>
internal sealed record ReviewCommand(ReviewMode Mode, bool Json, ReviewContextMapRequest? Request = null, string? Path = null);
