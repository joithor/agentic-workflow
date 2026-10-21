using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Core.Checks;

/// <summary>
/// The commands of the approved profile's <c language="text">workflow.build.commands</c>: each entry is one command line, split on spaces (no quoting), and runs on 'checks'.
/// The profile has no key for the red check's or the GREEN gate's commands yet, so those stages list none.
/// </summary>
/// <param name="profile">The approved profile.</param>
public sealed class ProfileBuildCommands(IApprovedProfile profile) : IBuildCommandSource
{
    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<BuildCommand>>> For(string stage, CancellationToken cancellationToken)
    {
        if (!string.Equals(stage, BuildCommand.Checks, StringComparison.Ordinal))
        {
            return Results.Success<IReadOnlyList<BuildCommand>>([]);
        }

        var latest = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        IReadOnlyList<BuildCommand> found = [.. (latest.Value?.Profile.Workflow?.Build?.Commands ?? []).Select(static line => new BuildCommand(line, line.Split(' ', StringSplitOptions.RemoveEmptyEntries)))];
        return Results.Success(found);
    }
}
