using System.Globalization;
using System.Security.Cryptography;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="ICommandNonces"/> as one owner-only file per nonce under '&lt;home&gt;/workflow/nonces' (engine spec rev 8 §5.2),
/// so a claim outlives the short-lived CLI process that made it. The file holds the SHA-256 of the command's subject, the claim time
/// (Unix milliseconds, from the injected clock) and, once the command applied, 'applied'. Creating it with
/// <see cref="FileMode.CreateNew"/> makes the claim atomic between processes. Engine P4 unit (c): the stand-in for API
/// P1's nonce table until 'IssueNonce' lands; expired files are not swept yet.
/// </summary>
/// <param name="home">Where the nonces directory is.</param>
/// <param name="clock">The clock the claim time and the 10-minute lifetime are read from.</param>
internal sealed class HomeCommandNonces(WorkflowsHome home, TimeProvider clock) : ICommandNonces
{
    private const string AppliedMark = "applied";
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerDirectory = OwnerFile | UnixFileMode.UserExecute;

    public CommandNonce Issue() => CommandNonce.From(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)));

    public async ValueTask<Result<NonceClaim>> Claim(CommandNonce nonce, string subject, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var digest = Digest(subject);
        var now = clock.GetUtcNow();
        try
        {
            _ = OperatingSystem.IsWindows() ? Directory.CreateDirectory(home.NoncesDirectory) : Directory.CreateDirectory(home.NoncesDirectory, OwnerDirectory);
            if (await TryCreate(FileOf(nonce), Text(digest, now), cancellationToken).ConfigureAwait(false))
            {
                return NonceClaim.Fresh;
            }

            var lines = await File.ReadAllLinesAsync(FileOf(nonce), cancellationToken).ConfigureAwait(false);
            if (lines is not [var claimed, var at, ..] || !long.TryParse(at, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds))
            {
                return WorkflowErrors.NonceRefused.With($"nonce {nonce.Value} has a damaged claim (fix: run the command again; the CLI fetches a new nonce)");
            }

            if (now - DateTimeOffset.FromUnixTimeMilliseconds(milliseconds) > ICommandNonces.Lifetime)
            {
                return NonceExpiry.Refusal(nonce, string.Equals(claimed, digest, StringComparison.Ordinal), lines.Contains(AppliedMark, StringComparer.Ordinal));
            }

            return string.Equals(claimed, digest, StringComparison.Ordinal)
                ? (lines.Contains(AppliedMark, StringComparer.Ordinal) ? NonceClaim.Applied : NonceClaim.Pending)
                : WorkflowErrors.NonceRefused.With($"nonce {nonce.Value} was used for another command (fix: run the command again; the CLI fetches a new nonce)");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) // boundary: an unwritable home is an expected failure
        {
            return Unwritable(e);
        }
    }

    public async ValueTask<Result<Unit>> Applied(CommandNonce nonce, string subject, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subject);
        try
        {
            // the claim can be gone (given back by a refused sibling, or removed by hand): the command applied, so record that
            if (!await TryCreate(FileOf(nonce), Text(Digest(subject), clock.GetUtcNow()) + AppliedMark + "\n", cancellationToken).ConfigureAwait(false))
            {
                await File.AppendAllTextAsync(FileOf(nonce), AppliedMark + "\n", cancellationToken).ConfigureAwait(false);
            }

            return Unit.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) // boundary: as in Claim
        {
            return Unwritable(e);
        }
    }

    public async ValueTask<Result<Unit>> Release(CommandNonce nonce, CancellationToken cancellationToken)
    {
        try
        {
            var file = FileOf(nonce);
            if (File.Exists(file) && !(await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false)).Contains(AppliedMark, StringComparer.Ordinal))
            {
                File.Delete(file);
            }

            return Unit.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) // boundary: as in Claim
        {
            return Unwritable(e);
        }
    }

    private static string Digest(string subject) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(subject)));   // one line, whatever the subject holds

    private static string Text(string digest, DateTimeOffset at) => $"{digest}\n{at.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}\n";

    private static async Task<bool> TryCreate(string file, string text, CancellationToken cancellationToken)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerFile;
        }

        FileStream stream;
        try
        {
#pragma warning disable CA2000 // disposed by the await using below
            stream = new FileStream(file, options);
#pragma warning restore CA2000
        }
        catch (IOException) when (File.Exists(file))
        {
            return false;
        }

        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(text), cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private string FileOf(CommandNonce nonce) => Path.Combine(home.NoncesDirectory, nonce.Value);

    private Error Unwritable(Exception e) =>
        SindriErrors.ApiInternal.With($"the nonce table under {home.NoncesDirectory} can't be read or written: {e.Message} (fix: check the Sindri home's permissions and free space)");
}
