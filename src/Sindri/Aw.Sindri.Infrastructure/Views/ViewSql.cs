using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>views.db's SQL. A view's table comes from this fixed map (<see cref="ViewNames"/>), never from input.</summary>
internal static class ViewSql
{
    /// <summary>Whether a table exists: views.db is created before its migration commits, so a reader can open it with no tables yet.</summary>
    internal const string TableExists = "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name)";

    internal const string StateTable = "projector_state";

    internal const string SelectState =
        "SELECT cursor, schema_version, projected_at, rebuilding, stalled_at, stalled_type FROM projector_state WHERE id = 1";

    internal const string InsertState =
        "INSERT INTO projector_state (id, cursor, schema_version, rebuilding) VALUES (1, 0, $schemaVersion, 0) ON CONFLICT (id) DO NOTHING";

    internal const string UpdateState =
        "UPDATE projector_state SET cursor = $cursor, projected_at = $projectedAt, stalled_at = $stalledAt, stalled_type = $stalledType WHERE id = 1";

    internal const string ResetState =
        "UPDATE projector_state SET cursor = 0, schema_version = $schemaVersion, projected_at = $at, rebuilding = 1, stalled_at = NULL, " +
        "stalled_type = NULL WHERE id = 1";

    internal const string FinishRebuild = "UPDATE projector_state SET rebuilding = 0 WHERE id = 1";

    private static readonly Dictionary<string, ViewTable> _tables = new(StringComparer.Ordinal)
    {
        [ViewNames.Services] = new ViewTable("view_services"),
        [ViewNames.Profile] = new ViewTable("view_profile"),
        [ViewNames.Items] = new ViewTable("view_items"),
        [ViewNames.ItemAliases] = new ViewTable("view_item_aliases"),
        [ViewNames.ItemTimeline] = new ViewTable("view_item_timeline"),
        [ViewNames.Requests] = new ViewTable("view_requests", Columns(item: true, state: true, repo: true, kind: false)),
        [ViewNames.Decisions] = new ViewTable("view_decisions", Columns(item: true, state: true, repo: true, kind: false)),
        [ViewNames.NeedsYou] = new ViewTable("view_needs_you", Columns(item: true, state: false, repo: true, kind: true)),
    };

    private static Dictionary<ViewColumn, string> Columns(bool item, bool state, bool repo, bool kind)
    {
        var columns = new Dictionary<ViewColumn, string>();
        if (item)
        {
            columns[ViewColumn.Item] = "item";
        }

        if (state)
        {
            columns[ViewColumn.State] = "state";
        }

        if (repo)
        {
            columns[ViewColumn.Repo] = "repo";
        }

        if (kind)
        {
            columns[ViewColumn.Kind] = "kind";
        }

        return columns;
    }

    /// <summary>The table behind <paramref name="view"/>, or null when it isn't a view.</summary>
    /// <param name="view">A <see cref="ViewNames"/> name.</param>
    /// <returns>The table.</returns>
    internal static ViewTable? Table(string view) => _tables.GetValueOrDefault(view);
}
