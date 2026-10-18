namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Where the owner-copy source reads the owner's login and writes the per-launch copies (sandbox spec §3.1, §3.4).</summary>
/// <param name="Home">The owner's home directory.</param>
/// <param name="SindriHome">AW_SINDRI_HOME: the copies live under 'launches/&lt;launch&gt;/login'.</param>
/// <param name="MinRemaining">How long the login must stay valid for a launch to start: the planned wall time plus a margin (spike item 2: assume a refresh rotates the token, so a session must never refresh).</param>
internal sealed record OwnerCopyOptions(string Home, string SindriHome, TimeSpan MinRemaining)
{
    /// <summary>The default margin: the 4 h default wall limit, the 10 minutes of wall grace and 15 minutes of margin.</summary>
    internal static readonly TimeSpan DefaultMinRemaining = TimeSpan.FromHours(4) + TimeSpan.FromMinutes(25);
}
