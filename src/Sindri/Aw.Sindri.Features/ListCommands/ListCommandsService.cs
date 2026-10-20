using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListCommands;

/// <summary>
/// Serves the command catalog (API spec §3.1, UI spec E-8), sorted by action id. The allowed callers come from the
/// authorization table; a terminal-bound row narrows them to a human at a terminal, as the chain decides it.
/// </summary>
/// <param name="catalog">The command catalog.</param>
internal sealed class ListCommandsService(ICommandCatalog catalog) : IService<ListCommandsRequest, ListCommandsResponse>
{
    private const string HumanAtTerminal = "human+terminal";

    public ValueTask<Result<ListCommandsResponse>> Handle(ListCommandsRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<CommandListRow> rows = [.. catalog.Rows.OrderBy(static row => row.Action.Value, StringComparer.Ordinal).Select(Listed)];
        return ValueTask.FromResult<Result<ListCommandsResponse>>(new ListCommandsResponse(rows));
    }

    private static CommandListRow Listed(CommandRow row) =>
        new(row.Action, row.Class, Callers(row), row.NeedsTerminal, row.Method, row.Route, row.Cli.Text, row.Ui, row.Destructive,
            row.ReadsViews, row.Shortcut, row.Confirm);

    private static IReadOnlyList<string> Callers(CommandRow row)
    {
        var callers = Authorization.CallersFor(row.Class, row.ServiceGrants);
        return row.NeedsTerminal ? [.. callers.Where(static c => string.Equals(c, HumanAtTerminal, StringComparison.Ordinal))] : callers;
    }
}
