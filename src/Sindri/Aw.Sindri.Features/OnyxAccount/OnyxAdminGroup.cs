using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxAccount;

internal sealed class OnyxAdminGroup(IOnyxDatabase database, OnyxFacts facts)
{
    internal ValueTask<Result<int>> Find(IOnyxSession session, CancellationToken cancellationToken)
    {
        _ = database;
        _ = facts;
        return session.AdminGroup(cancellationToken);
    }
}
