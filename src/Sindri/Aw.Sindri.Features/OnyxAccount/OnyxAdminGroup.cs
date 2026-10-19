using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxAccount;

/// <summary>
/// Which group Sindri's key joins. Onyx's own user-group list answers it, except on the Community Edition, where that route is Enterprise-only and answers 402 (SND-ONYX-013 from the session):
/// then the default Admin group's id is read from the database of the stack Sindri itself runs (<see cref="IOnyxDatabase"/>). Nothing else falls back: a refused login, a failed request
/// or a list with no Admin group is returned as it is. An Onyx with no stack of Sindri's here (the owner's own, the manual setup) is never read.
/// </summary>
internal sealed class OnyxAdminGroup(IOnyxDatabase database, OnyxFacts facts)
{
    private const string NoStack = "Onyx's Community Edition has no user-group list (HTTP 402), and no Onyx stack of Sindri's runs on this box to read the Admin group from; "
        + "make the key by hand as docs/sindri/onyx.md section 5 says (Admin → API Keys, named sindri-onyx), put it in the profile by pointer, and approve the profile";

    internal async ValueTask<Result<int>> Find(IOnyxSession session, CancellationToken cancellationToken)
    {
        var listed = await session.AdminGroup(cancellationToken).ConfigureAwait(false);
        if (listed.IsSuccess || !string.Equals(listed.Error.Code, SindriErrors.OnyxGroupListUnavailable.Code, StringComparison.Ordinal))
        {
            return listed;
        }

        if (facts.DockerProblem(cancellationToken) is { } problem)
        {
            return problem;   // the local-daemon guard every docker command goes through: nothing is sent to a missing, stopped or remote Docker
        }

        var group = database.DefaultAdminGroup(facts.Project, cancellationToken);
        if (group.IsError)
        {
            return group.Error;
        }

        return group.Value is { } id ? id : SindriErrors.OnyxKeyFailed.With(NoStack);
    }
}
