using Aw.Meta.Features.Roles;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'role-show' command: exit 1 when no meta session is registered.</summary>
internal static class RoleShowCommand
{
    internal static CommandEntry Entry { get; } = new(
        "role-show",
        CommandSpec.None("role-show [--json]"),
        NeedsConfig: false,
        static context => CommandOutput.Call<ShowRoleRequest, ShowRoleResponse>(
            context,
            new ShowRoleRequest(),
            static response => response.Role is { } role ? RoleSetCommand.Text(role) : "no meta session registered",
            static response => response.Role is null));
}
