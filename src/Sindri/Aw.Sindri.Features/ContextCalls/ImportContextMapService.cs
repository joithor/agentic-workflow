using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Shows or performs a map import (context spec §8.7), a protected command (engine spec §4.2, README X30): without a hash,
/// the checked file, its content hash and the server's challenge 'import map &lt;hash6&gt;' bound to that hash; with a hash,
/// a file that changed since is SND-CONTEXT-034, then the answer must redeem that challenge (missing, mistyped, used,
/// expired or for another hash: SND-AUTH-002), the engine imports and 'context.map.imported' is appended. The chain has
/// already refused anyone but a human at a terminal.
/// </summary>
internal sealed class ImportContextMapService(ContextScopes scopes, IContextEngine engine, HeldLease held, ILedgerStore ledger, EventFactory events, IChallenges challenges)
    : IService<ImportContextMapRequest, ImportContextMapResponse>
{
    public async ValueTask<Result<ImportContextMapResponse>> Handle(ImportContextMapRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var preview = await engine.PreviewMap(request.Path, scope.Value, cancellationToken).ConfigureAwait(false);
        if (preview.IsError || request.Hash is not { } hash)
        {
            return preview.IsError ? preview.Error : Previewed(preview.Value);
        }

        if (Refusal(hash, preview.Value.Hash, request.Answer) is { } refused)
        {
            return refused;
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var imported = await engine.ImportMap(request.Path, hash, scope.Value, cancellationToken).ConfigureAwait(false);
        if (imported.IsError)
        {
            return imported.Error;
        }

        var v = imported.Value;
        var payload = new ContextMapImported(v.Version.Number, v.Version.Repo.Value, v.Hash, v.Counts.Nodes, v.Counts.Edges, v.Counts.Overrides);
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextMapImported}:{v.Hash}:v{v.Version.Number}");
        var appended = await ledger.Append(events.Entry(EventTypes.ContextMapImported, payload, key), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new ImportContextMapResponse(preview.Value, v, appended.Value.Sequence);
    }

    private ImportContextMapResponse Previewed(MapFilePreview preview)
    {
        var issued = challenges.Issue(Action(preview.Hash), Subject(preview.Hash));
        return new ImportContextMapResponse(preview, Imported: null, Sequence: null, new MapImportChallenge(issued.Id, issued.Action.Text, issued.ExpiresAt));
    }

    /// <summary>A hash that isn't the file's now (034); no answer, or one that doesn't redeem the challenge for this hash (SND-AUTH-002).</summary>
    private Error? Refusal(string asked, string live, ChallengeAnswer? answer) => true switch
    {
        _ when !string.Equals(asked, live, StringComparison.Ordinal) => ContextErrors.MapFileChanged.With(
            $"the file's content hash is now {live}, not the one you confirmed; nothing was written (fix: run 'sindri context map import <file>' again)"),
        _ when answer is null => SindriErrors.AuthRefused.With(
            $"importing a map needs you at an interactive terminal: run 'sindri context map import <file>' and type '{Action(live).Text}'"),
        _ => challenges.Redeem(answer, Action(live), Subject(live), SindriErrors.AuthRefused) is { IsError: true } stale ? stale.Error : null,
    };

    /// <summary>'import map &lt;hash6&gt;': the text the human types.</summary>
    private static ChallengeAction Action(string hash) => new("import map " + hash[..Math.Min(6, hash.Length)]);

    /// <summary>The map file's whole content hash: a file that changes after the preview no longer matches.</summary>
    private static ChallengeSubject Subject(string hash) => new("map:" + hash, Digest: null);
}
