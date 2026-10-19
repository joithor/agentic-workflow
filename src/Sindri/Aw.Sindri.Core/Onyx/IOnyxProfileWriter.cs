using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>Writes the `context.sources.onyx` section of the live profile, keeping every other key and comment as they were.</summary>
public interface IOnyxProfileWriter
{
    /// <summary>Sets the section, creating the profile (`schemaVersion: 1`, `user`) when there is none.</summary>
    /// <param name="url">http://127.0.0.1:&lt;port&gt;.</param>
    /// <param name="token">The secret pointer to the key.</param>
    /// <param name="version">The pinned release.</param>
    /// <returns>True when the file changed; false when the section was already as given.</returns>
    public Result<bool> SetOnyx(string url, string token, string version);
}
