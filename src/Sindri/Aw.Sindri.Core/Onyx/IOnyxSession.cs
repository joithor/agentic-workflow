using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>A logged-in Onyx session; disposing it logs out and drops the cookie.</summary>
public interface IOnyxSession : IAsyncDisposable
{
    /// <summary>Finds the id of the default Admin group (`GET /api/manage/admin/user-group?include_default=true`).</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The id; SND-ONYX-007 when the login isn't an admin (Onyx refuses the list), SND-ONYX-008 when there is no Admin group.</returns>
    public ValueTask<Result<int>> AdminGroup(CancellationToken cancellationToken);

    /// <summary>Makes the API key (`POST /api/admin/api-key`, `{name, group_ids}`); the raw key is returned once.</summary>
    /// <param name="name">The key's name (`sindri-onyx`).</param>
    /// <param name="groupId">The Admin group.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The raw key, or SND-ONYX-007 or SND-ONYX-008.</returns>
    public ValueTask<Result<string>> CreateKey(string name, int groupId, CancellationToken cancellationToken);
}
