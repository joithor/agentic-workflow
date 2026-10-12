namespace Aw.Sindri.Core.Secrets;

/// <summary>Where a secret pointer's value lives (context spec §4 "Secrets").</summary>
public enum SecretScheme
{
    /// <summary>An environment variable: 'env:NAME'.</summary>
    Env = 1,

    /// <summary>A file readable by its owner only (no group or other permission bits): 'file:/absolute/path'.</summary>
    File = 2,

    /// <summary>A macOS keychain generic password: 'keychain:&lt;service&gt;/&lt;account&gt;'.</summary>
    Keychain = 3,

    /// <summary>A 1Password field read through the 'op' CLI: 'op:&lt;vault&gt;/&lt;item&gt;/&lt;field&gt;'.</summary>
    OnePassword = 4,
}
