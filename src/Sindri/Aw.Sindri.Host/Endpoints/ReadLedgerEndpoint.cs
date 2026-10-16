using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ReadLedger;

namespace Aw.Sindri.Host.Endpoints;

internal static class ReadLedgerEndpoint
{
    internal static RouteGroupBuilder MapReadLedger(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/ledger", static async (long? after, string? item, string? type, int? max, IService<ReadLedgerRequest, LedgerPage> service, CancellationToken ct) =>
        {
            ItemRef? itemRef = null;
            if (!string.IsNullOrEmpty(item))
            {
                var parsed = ItemRoute.Item(item);
                if (parsed.IsError)
                {
                    return parsed.ToHttpResult();
                }

                itemRef = parsed.Value;
            }

            var request = new ReadLedgerRequest(after ?? 0, itemRef, type, max ?? 100);
            return (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
