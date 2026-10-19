using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// A read of the Postgres that Sindri's own Onyx stack runs, for the one fact the Community Edition's HTTP API doesn't give (its user-group list is Enterprise-only, so it answers 402):
/// the id of the default Admin group, which `POST /api/admin/api-key` needs to give a key's service account the admin role. Read-only, one fixed query, and only for a stack
/// under a compose project Sindri named; an Onyx the owner runs themselves is never touched.
/// </summary>
public interface IOnyxDatabase
{
    /// <summary>Reads the id of the default Admin group (`select id from user_group where is_default and name = 'Admin'`) from the project's `relational_db` container.</summary>
    /// <param name="project">The compose project of this Sindri home (see <see cref="ComposeProject.NameFor"/>); a name Sindri didn't make is refused.</param>
    /// <param name="cancellationToken">Kills the command.</param>
    /// <returns>The id; null when the project has no running database container (no stack of Sindri's runs here); SND-ONYX-001 or 011 when docker fails, SND-ONYX-008 when the database has no single default Admin group.</returns>
    public Result<int?> DefaultAdminGroup(ComposeProject project, CancellationToken cancellationToken);
}
