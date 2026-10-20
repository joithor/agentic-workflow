using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.Commands;

/// <summary>
/// The chain's authorization (API spec §2.1, §4.2): classifies the attempt (fail-closed, UI spec E-12) and checks its
/// class against the caller before validation, so an unauthorized caller learns nothing about valid input. A refusal is
/// SND-AUTH-002 naming the command and its CLI; the audit around it writes the only row. The prover (human:prover) confirms an
/// AppConfirm row only in a scratch home that holds the marker only the prover writes (setup-once spec §4 item 6).
/// </summary>
/// <param name="inner">Validation and the handler.</param>
/// <param name="callers">The caller of this attempt.</param>
/// <param name="catalog">The command catalog.</param>
/// <param name="prover">Whether this home lets the prover confirm what the app confirms; none counts as not.</param>
/// <typeparam name="TRequest">The command record.</typeparam>
/// <typeparam name="TResponse">What it answers.</typeparam>
internal sealed class AuthorizingService<TRequest, TResponse>(IService<TRequest, TResponse> inner, ICallerContextAccessor callers, ICommandCatalog catalog, ProverLabel? prover = null)
    : Decorated<TRequest, TResponse>(inner)
    where TRequest : notnull
{
    private const string ProverActor = "human:prover";

    /// <summary>The service it wraps (for the chain-order test).</summary>
    internal IService<TRequest, TResponse> Next => Inner;

    public override ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var row = ActionClassifier.Classify(catalog, request);
        var refusal = Authorization.Decide(row.Class, callers.Current, row).Refusal ?? ProverRefusal(row);
        return refusal is not null
            ? ValueTask.FromResult<Result<TResponse>>(Named(request, row, refusal))
            : Inner.Handle(request, cancellationToken);
    }

    /// <summary>The prover is honoured on an AppConfirm row only in a marked scratch home; the owner's default home never carries the marker.</summary>
    private Error? ProverRefusal(CommandRow row) =>
        row.AppConfirm && callers.Current is { } caller && string.Equals(caller.Actor, ProverActor, StringComparison.Ordinal) && prover is not { MayConfirm: true }
            ? SindriErrors.AuthRefused.With($"{row.Action.Value} is a human decision, and the prover confirms it only in a scratch home it created: {row.Cli.Text}")
            : null;

    /// <summary>A row shared by several commands (the challenge row: approve and revoke) refuses with the one command the request asked for.</summary>
    private static Error Named(TRequest request, CommandRow row, Error refusal) =>
        request is IRefusalCli { RefusalCli: { } cli }
            ? refusal with { Message = refusal.Message.Replace(row.Cli.Text, cli, StringComparison.Ordinal) }
            : refusal;
}
