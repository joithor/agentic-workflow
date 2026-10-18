using Aw.Kernel;

namespace Aw.Sindri.Testing.Sandbox;

/// <summary>Reads the 'reason' and 'source' a sandbox refusal carries (one place, so a change in how SandboxRefusal exposes them is one edit).</summary>
public static class SandboxErrors
{
    /// <summary>The refusal's reason.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The reason, or null.</returns>
    public static string? Reason(Error error) => Extension(error, "reason");

    /// <summary>The refusal's credential source.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The source id, or null.</returns>
    public static string? Source(Error error) => Extension(error, "source");

    private static string? Extension(Error error, string name)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Extensions?.Where(e => string.Equals(e.Key, name, StringComparison.Ordinal)).Select(e => e.Value.GetString()).FirstOrDefault();
    }
}
