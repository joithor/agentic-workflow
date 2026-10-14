using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The API's own nonce table (port; engine spec rev 8 §5.2): a command's nonce is the Update's id, so Temporal returns
/// the first outcome for a repeated one; this table refuses a nonce used for a <b>different</b> command, or one older
/// than <see cref="Lifetime"/>, with SND-WF-009 before any Update is sent. A command is named by its subject, a digest of
/// the instance and everything the command says.
/// </summary>
public interface ICommandNonces
{
    /// <summary>How long a nonce lives: 10 minutes (spec §5.2).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// A new nonce. Engine P4 unit (c): the stand-in for API P1's 'POST /workflow/instances/{a}/nonce' (IssueNonce),
    /// which isn't built yet; the slice calls it when the caller gave no '--nonce'.
    /// </summary>
    /// <returns>128 random bits as 32 lower-case hex digits.</returns>
    public CommandNonce Issue();

    /// <summary>Claims <paramref name="nonce"/> for the command <paramref name="subject"/> names.</summary>
    /// <param name="nonce">The nonce.</param>
    /// <param name="subject">The command's digest.</param>
    /// <param name="cancellationToken">Cancels the claim.</param>
    /// <returns>
    /// <see cref="NonceClaim.Fresh"/> when this call created the claim, <see cref="NonceClaim.Pending"/> when the same command
    /// holds it but isn't recorded as applied (a send may have gone out), <see cref="NonceClaim.Applied"/> when it applied
    /// (the CLI prints 'already applied'); SND-WF-009 when the nonce was claimed for another command or is older than
    /// <see cref="Lifetime"/>.
    /// </returns>
    public ValueTask<Result<NonceClaim>> Claim(CommandNonce nonce, string subject, CancellationToken cancellationToken);

    /// <summary>Records that the command claimed with <paramref name="nonce"/> was applied.</summary>
    /// <param name="nonce">The nonce.</param>
    /// <param name="subject">The command's digest, as given to <see cref="Claim"/>; the claim is recreated as applied if it is gone.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Done.</returns>
    public ValueTask<Result<Unit>> Applied(CommandNonce nonce, string subject, CancellationToken cancellationToken);

    /// <summary>
    /// Gives back a claim whose command was refused, so a rejected command consumes nothing (spec §5.2, W12). Only the call
    /// whose <see cref="Claim"/> was <see cref="NonceClaim.Fresh"/> may, and only while nothing it sent is unconfirmed.
    /// </summary>
    /// <param name="nonce">The nonce.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Done.</returns>
    public ValueTask<Result<Unit>> Release(CommandNonce nonce, CancellationToken cancellationToken);
}
