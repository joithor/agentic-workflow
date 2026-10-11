namespace Aw.Evidence.Core.Publishing;

/// <summary>The argv for a signed PUT: every header verbatim, the bytes untouched. Run it within 60 s of the prepare step.</summary>
public static class PutCommand
{
    /// <summary>Builds the curl argv.</summary>
    /// <param name="upload">The signed target.</param>
    /// <param name="file">The file to upload.</param>
    /// <returns>The argv.</returns>
    public static IReadOnlyList<string> Build(SignedUpload upload, string file) =>
        ["curl", "-sS", "--fail", "-X", "PUT", "--data-binary", $"@{file}",
         .. upload.Headers.SelectMany(static h => new[] { "-H", $"{h.Key}: {h.Value}" }),
         upload.Url];
}
