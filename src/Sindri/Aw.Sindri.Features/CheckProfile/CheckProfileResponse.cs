namespace Aw.Sindri.Features.CheckProfile;

/// <summary>What the check recorded.</summary>
/// <param name="Sequence">The 'profile.checked' event's ledger position, or null when the pair was unchanged.</param>
public sealed record CheckProfileResponse(long? Sequence);
