namespace Aw.Sindri.Features.ManifestRecipes;

/// <summary>'recipe manifest &lt;dir&gt;' (setup-once spec §5.3): the manifest of the recipe files in one directory, in process.</summary>
/// <param name="Directory">The directory, laid out like <c language="text">recipes/sindri</c>.</param>
public sealed record ManifestRecipesRequest(string Directory);
