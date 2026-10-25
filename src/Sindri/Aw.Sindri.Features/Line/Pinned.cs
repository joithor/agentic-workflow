using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Features.Line;

/// <summary>What <see cref="StylePin.Read"/> found.</summary>
/// <param name="Pack">The pinned pack; null when the run pinned none, or the pin failed.</param>
/// <param name="Failure">Why the pin can't be honoured; null when it can.</param>
internal sealed record Pinned(StylePack? Pack, string? Failure);
