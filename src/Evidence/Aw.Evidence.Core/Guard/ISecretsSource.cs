namespace Aw.Evidence.Core.Guard;

/// <summary>Reads secrets (sign-in credentials) by name; the environment is the only source today.</summary>
public interface ISecretsSource
{
    /// <summary>The secret named <paramref name="name"/>, or null when it is unset or blank.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The value, or null.</returns>
    public string? Get(string name);
}
