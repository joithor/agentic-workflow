namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary>Where the built-in style packs' files come from: the toolkit's own <c language="text">config/packs/style</c>, embedded in the build.</summary>
internal interface IStylePackSource
{
    /// <summary>The files of the built-in pack.</summary>
    /// <param name="id">The pack id.</param>
    /// <returns>Each file's path inside the pack ('/' separated) and its text; null when there is no such pack.</returns>
    public IReadOnlyDictionary<string, string>? Files(string id);

    /// <summary>The ids of the built-in packs, in id order.</summary>
    /// <returns>The ids.</returns>
    public IReadOnlyList<string> Ids();
}
