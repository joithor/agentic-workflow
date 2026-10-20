namespace Aw.Sindri.Core.Setup;

/// <summary>Where the registered recipes stand: each recipe counted once, at its latest version.</summary>
/// <param name="Total">Recipes that can be approved (revoked ones are left out).</param>
/// <param name="Approved">Of those, the ones approved at their current hash.</param>
public sealed record RecipesFact(int Total, int Approved);
