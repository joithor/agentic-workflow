using Aw.Kernel;

namespace Aw.Sindri.Core.Secrets;

/// <summary>
/// A pointer to a secret held outside the profile (context spec §4 "Secrets", §8.8): 'env:NAME',
/// 'file:/absolute/path', 'keychain:&lt;service&gt;/&lt;account&gt;' or 'op:&lt;vault&gt;/&lt;item&gt;/&lt;field&gt;'.
/// The target is not secret; the value it points at is.
/// </summary>
public sealed record SecretPointer
{
    private SecretPointer(SecretScheme scheme, string target)
    {
        Scheme = scheme;
        Target = target;
    }

    /// <summary>Where the value lives.</summary>
    public SecretScheme Scheme { get; }

    /// <summary>The scheme's target: the variable name, the path, 'service/account' or 'vault/item/field'.</summary>
    public string Target { get; }

    /// <summary>Whether a name passed to the environment function is a pointer: it holds a ':', which no environment variable name does.</summary>
    /// <param name="name">The name.</param>
    /// <returns>True for a pointer.</returns>
    public static bool IsPointer(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Contains(':', StringComparison.Ordinal);
    }

    /// <summary>Parses a pointer. The error never repeats the text after the scheme (it may be a secret pasted by mistake).</summary>
    /// <param name="text">The pointer text from the profile.</param>
    /// <returns>The pointer, or SND-SECRET-001.</returns>
    public static Result<SecretPointer> Parse(string text) => throw new NotSupportedException($"red: parse {new SecretPointer(SecretScheme.Env, text)}");
}
