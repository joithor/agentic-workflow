namespace Aw.Meta.Infrastructure.Build;

/// <summary>Reads the commit out of the InformationalVersion the SDK writes when a build passes SourceRevisionId ("1.0.0+&lt;commit&gt;").</summary>
internal static class BuildStamp
{
    /// <summary>The commit after the first '+', or "unknown" when the version carries none.</summary>
    /// <param name="informationalVersion">The assembly's InformationalVersion.</param>
    /// <returns>The commit.</returns>
    internal static string Commit(string informationalVersion) => throw new NotSupportedException();
}
