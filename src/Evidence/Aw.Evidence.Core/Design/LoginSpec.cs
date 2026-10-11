using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Design;

/// <summary>How to sign in: CSS selectors for the form; the credentials come only from the named environment variables.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LoginSpec
{
    /// <summary>Creates a sign-in spec; JSON reads through this constructor so its defaults apply to missing keys.</summary>
    /// <param name="emailEnv">The environment variable holding the email.</param>
    /// <param name="passwordEnv">The environment variable holding the password.</param>
    /// <param name="emailSelector">The email field.</param>
    /// <param name="passwordSelector">The password field.</param>
    /// <param name="submitSelector">The submit control.</param>
    /// <param name="nextSelector">Clicked after the email on two-step forms.</param>
    /// <param name="loginPath">Sign-in is done once the URL path no longer contains this; default '/login'.</param>
    [JsonConstructor]
    public LoginSpec(string emailEnv, string passwordEnv, string emailSelector, string passwordSelector, string submitSelector, string? nextSelector = null, string? loginPath = null)
    {
        EmailEnv = emailEnv;
        PasswordEnv = passwordEnv;
        EmailSelector = emailSelector;
        PasswordSelector = passwordSelector;
        SubmitSelector = submitSelector;
        NextSelector = nextSelector;
        LoginPath = loginPath ?? "/login";
    }

    /// <summary>The environment variable holding the email (UPPER_SNAKE).</summary>
    public string EmailEnv { get; init; }

    /// <summary>The environment variable holding the password (UPPER_SNAKE).</summary>
    public string PasswordEnv { get; init; }

    /// <summary>The email field.</summary>
    public string EmailSelector { get; init; }

    /// <summary>The password field.</summary>
    public string PasswordSelector { get; init; }

    /// <summary>The submit control.</summary>
    public string SubmitSelector { get; init; }

    /// <summary>Clicked after the email on two-step forms.</summary>
    public string? NextSelector { get; init; }

    /// <summary>Sign-in is done once the URL path no longer contains this.</summary>
    public string LoginPath { get; init; }
}
