namespace Aw.Evidence.Core.Guard;

/// <summary>
/// Reads named values from the environment: sign-in credentials and the UI_EVIDENCE_* settings (user agent, app title,
/// seed policy, visual model). The environment is the only source today.
/// </summary>
public interface ISecretsSource
{
    /// <summary>The secret named <paramref name="name"/>, or null when it is unset or blank.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The value, or null.</returns>
    public string? Get(string name);
}
