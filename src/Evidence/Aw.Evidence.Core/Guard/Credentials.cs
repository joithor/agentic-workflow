using Aw.Kernel;

namespace Aw.Evidence.Core.Guard;

/// <summary>Sign-in credentials read from named secrets. <see cref="ToString"/> never shows them.</summary>
public sealed class Credentials
{
    private Credentials(string email, string password)
    {
        Email = email;
        Password = password;
    }

    /// <summary>The email.</summary>
    public string Email { get; }

    /// <summary>The password.</summary>
    public string Password { get; }

    /// <summary>Reads both credentials; a missing one is an error that names the variables, never values.</summary>
    /// <param name="secrets">The source.</param>
    /// <param name="emailName">The email's variable name.</param>
    /// <param name="passwordName">The password's variable name.</param>
    /// <returns>The credentials, or AW-EVIDENCE-006.</returns>
    public static Result<Credentials> Read(ISecretsSource secrets, string emailName, string passwordName)
    {
        var email = secrets.Get(emailName);
        var password = secrets.Get(passwordName);
        string[] missing = [.. new[] { (emailName, email), (passwordName, password) }
            .Where(static p => string.IsNullOrEmpty(p.Item2)).Select(static p => p.Item1)];
        return missing.Length > 0 || email is null || password is null
            ? EvidenceErrors.CredentialsMissing.With($"missing credentials in environment: {string.Join(", ", missing)}")
            : new Credentials(email, password);
    }

    /// <summary>Replaces every credential in <paramref name="text"/> with [redacted], so an echoed error never leaks one.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The redacted text.</returns>
    public string Redact(string text) => text.Replace(Email, "[redacted]", StringComparison.Ordinal).Replace(Password, "[redacted]", StringComparison.Ordinal);

    /// <inheritdoc/>
    public override string ToString() => "Credentials([redacted])";
}
