using System.Globalization;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Features.Roles;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'role-set' command.</summary>
internal static class RoleSetCommand
{
    internal static CommandEntry Entry { get; } = new(
        "role-set",
        new CommandSpec("role-set --session <uuid> [--name <name>] [--json]", ["session"], ["name"], []),
        NeedsConfig: false,
        Run);

    private static Task<int> Run(CommandContext context)
    {
        if (!SessionId.TryParse(context.Line.Value("session"), CultureInfo.InvariantCulture, out var session))
        {
            return Task.FromResult(CommandOutput.Usage(context.Io, "--session must not be blank", Entry.Spec.Usage));
        }

        return CommandOutput.Call<SetRoleRequest, MetaRole>(context, new SetRoleRequest(session, context.Line["name"] ?? string.Empty), Text);
    }

    internal static string Text(MetaRole role) =>
        role.Name.Length == 0 ? $"meta session {role.Session.Value}" : $"meta session {role.Session.Value} ({role.Name})";
}
