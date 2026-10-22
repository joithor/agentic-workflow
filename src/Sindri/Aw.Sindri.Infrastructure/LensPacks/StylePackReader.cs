using Aw.Kernel;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary>Reads a style pack from its files (line spec §5.1): <c language="text">pack.json</c> with its sections and rules, and the template, voice and example files it names.</summary>
internal static class StylePackReader
{
    /// <summary>The pack the files hold.</summary>
    /// <param name="id">The id the pack was asked for; the file must say the same.</param>
    /// <param name="files">Each file's path inside the pack and its text.</param>
    /// <returns>The pack; an error naming what is wrong with the file.</returns>
    internal static Result<StylePack> Read(string id, IReadOnlyDictionary<string, string> files) => throw new NotSupportedException();
}
