namespace Aw.Meta.Features.Owner;

/// <summary>The ListOwner request.</summary>
/// <param name="All">Whether to list answered questions too.</param>
public sealed record ListOwnerRequest(bool All);
