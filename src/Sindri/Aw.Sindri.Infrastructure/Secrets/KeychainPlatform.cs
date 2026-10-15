namespace Aw.Sindri.Infrastructure.Secrets;

/// <summary>Which store a keychain: pointer reads: the macOS keychain, libsecret on Linux, or none.</summary>
internal enum KeychainPlatform
{
    /// <summary>No supported store: keychain: doesn't resolve.</summary>
    Unsupported,

    /// <summary>The macOS keychain, through 'security'.</summary>
    MacOS,

    /// <summary>libsecret (the desktop keyring), through 'secret-tool'.</summary>
    Linux,
}
