using System.Globalization;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Shows or performs a map import (context spec §8.7), shaped like 'profile approve': without a hash, the checked file and
/// its content hash; with a hash and the typed confirmation, the engine imports (refusing a file that changed since,
/// SND-CONTEXT-034) and 'context.map.imported' is appended. The chain has already refused anyone but a human at a terminal.
/// </summary>
internal sealed class ImportContextMapService(ContextScopes scopes, IContextEngine engine, HeldLease held, ILedgerStore ledger, EventFactory events)
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
            return preview.IsError ? preview.Error : new ImportContextMapResponse(preview.Value, Imported: null, Sequence: null);
        }

        if (!request.Confirmed)
        {
            return SindriErrors.AuthRefused.With(
                "importing a map needs you at an interactive terminal: run 'sindri context map import <file>' and type the first 6 characters of its hash");
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
}
