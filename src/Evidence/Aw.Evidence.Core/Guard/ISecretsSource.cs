namespace Aw.Evidence.Core.Guard;

/// <summary>
/// Reads secrets by the name a caller is given: the sign-in credentials a design manifest's login names
/// (emailEnv, passwordEnv). Settings with fixed names are <see cref="Settings.IEvidenceSettings"/>.
/// </summary>
public interface ISecretsSource
{
    /// <summary>The secret named <paramref name="name"/>, or null when it is unset or blank.</summary>
    /// <param name="name">The secret's name (an environment variable today).</param>
    /// <returns>The value, or null.</returns>
    public string? Get(string name);
}
