using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Infrastructure.Secrets;

/// <summary>
/// ISecretPointers over the environment, owner-only files, the macOS keychain ('security') and the 1Password CLI ('op').
/// Each pointer reads only the source it names: no fallback to another variable or tool. Values are never logged or put
/// in an error message.
/// </summary>
/// <param name="environment">Reads an env: pointer's variable.</param>
/// <param name="runner">Runs security, secret-tool and op.</param>
/// <param name="platform">Which store keychain: pointers read; null takes this machine's.</param>
internal sealed class PointerSecrets(Func<string, string?> environment, IToolRunner runner, KeychainPlatform? platform = null) : ISecretPointers
{
    private readonly KeychainPlatform _platform = platform ?? Detect();

    // The keychain answers in milliseconds; a locked keychain's prompt must not hold Sindri. op may sign in first.
    private static readonly TimeSpan _keychainTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _opTimeout = TimeSpan.FromSeconds(30);

    private const UnixFileMode GroupOrOther = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    /// <inheritdoc />
    public Result<string> Resolve(SecretPointer secretPointer)
    {
        ArgumentNullException.ThrowIfNull(secretPointer);
        return secretPointer.Scheme switch
        {
            SecretScheme.Env => Env(secretPointer),
            SecretScheme.File => FromFile(secretPointer),
            SecretScheme.Keychain => Keychain(secretPointer),
            SecretScheme.OnePassword => OnePassword(secretPointer),
            _ => throw new ArgumentOutOfRangeException(nameof(secretPointer), secretPointer.Scheme, "unknown scheme"),
        };
    }

    private static KeychainPlatform Detect()
        => OperatingSystem.IsMacOS() ? KeychainPlatform.MacOS : OperatingSystem.IsLinux() ? KeychainPlatform.Linux : KeychainPlatform.Unsupported;

    private Result<string> Env(SecretPointer secretPointer)
        => environment(secretPointer.Target) is { Length: > 0 } value
            ? value
            : Unresolved(secretPointer, "the variable is unset or empty; set it before starting Sindri");

    private static Result<string> FromFile(SecretPointer secretPointer)
    {
        try
        {
            return CheckedFile(secretPointer);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Unresolved(secretPointer, $"{secretPointer.Target} can't be checked or read ({e.GetType().Name}); check its directory's and its own owner and mode");
        }
    }

    private static Result<string> CheckedFile(SecretPointer secretPointer)
    {
        var path = secretPointer.Target;
        if (new FileInfo(path).LinkTarget is not null || Directory.Exists(path))
        {
            // A link (even to a 0600 file) is refused, not followed: its target's mode isn't what the pointer names, and
            // the target can change under it. Only the last path segment is checked (macOS's /tmp and /var are links).
            return SindriErrors.SecretFileUnsafe.With($"The {secretPointer} secret is unsafe: {path} is not a regular file (a link or a directory). Point it at a file holding only the secret, mode 0600.");
        }

        if (!File.Exists(path))
        {
            return Unresolved(secretPointer, $"{path} doesn't exist; create it with mode 0600");
        }

        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & GroupOrOther) != UnixFileMode.None)
        {
            return SindriErrors.SecretFileUnsafe.With($"The {secretPointer} secret is unsafe: {path} is readable or writable by others than its owner. Run 'chmod 600 {path}'.");
        }

        return NonEmpty(secretPointer, TrimNewline(File.ReadAllText(path)), $"{path} is empty; write the secret into it");
    }

    private Result<string> Keychain(SecretPointer secretPointer)
    {
        _ = _platform;   // red stub: every platform still reads 'security'
        var parts = secretPointer.Target.Split('/');
        return FromTool(secretPointer, "security", ["find-generic-password", "-s", parts[0], "-a", parts[1], "-w"], _keychainTimeout,
            $"add it with 'security add-generic-password -s {parts[0]} -a {parts[1]} -w' (macOS keychain)");
    }

    private Result<string> OnePassword(SecretPointer secretPointer)
        => FromTool(secretPointer, "op", ["read", $"op://{secretPointer.Target}"], _opTimeout,
            "check the vault, item and field, and that the 1Password CLI is installed and signed in");

    private Result<string> FromTool(SecretPointer secretPointer, string tool, string[] arguments, TimeSpan timeout, string fix)
    {
        var run = runner.Run(tool, arguments, timeout);
        var why = run.Outcome switch
        {
            ToolOutcome.Missing => $"'{tool}' isn't on PATH",
            ToolOutcome.TimedOut => string.Create(CultureInfo.InvariantCulture, $"'{tool}' didn't answer within {timeout.TotalSeconds:0} s (a locked keychain or a sign-in prompt?)"),
            ToolOutcome.Exited when run.ExitCode != 0 => string.Create(CultureInfo.InvariantCulture, $"'{tool}' exited with {run.ExitCode} (no such entry?)"),
            ToolOutcome.Exited => null,
            _ => throw new ArgumentOutOfRangeException(nameof(tool), run.Outcome, "unknown outcome"),
        };
        return why is null
            ? NonEmpty(secretPointer, TrimNewline(run.Output), $"'{tool}' printed an empty value; {fix}")
            : Unresolved(secretPointer, $"{why}; {fix}");
    }

    private static Result<string> NonEmpty(SecretPointer secretPointer, string value, string why)
        => value.Length > 0 ? value : Unresolved(secretPointer, why);

    // Removes one trailing line ending (what echo, editors and the tools add), nothing else.
    private static string TrimNewline(string value)
        => value.EndsWith("\r\n", StringComparison.Ordinal) ? value[..^2]
            : value.EndsWith('\n') ? value[..^1]
            : value;

    private static Error Unresolved(SecretPointer secretPointer, string why)
        => SindriErrors.SecretUnresolved.With($"The {secretPointer} secret doesn't resolve: {why}.");
}
