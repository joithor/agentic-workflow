using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Checks;

/// <summary>
/// The commands of the approved profile's <c language="text">workflow.build.commands</c>: each entry is one command line (<see cref="CommandLine"/>), each may run as long as <c language="text">workflow.steps.longMax</c>.
/// With no profile approved, or no commands in it, the set is empty.
/// </summary>
/// <param name="profile">The approved profile.</param>
public sealed class ProfileBuildCommands(IApprovedProfile profile) : IBuildCommandSource
{
    /// <inheritdoc />
    public async ValueTask<Result<CommandSet>> Read(CancellationToken cancellationToken)
    {
        var latest = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        if (latest.Value?.Profile is not { } approved)
        {
            return new CommandSet([], DefaultEach);
        }

        var each = RecipeDuration.TryFrom(LineProfile.LongMax(approved), out var longest) ? longest.Span : DefaultEach;
        var commands = new List<BuildCommand>();
        foreach (var line in approved.Workflow?.Build?.Commands ?? [])
        {
            // Profile validation refuses a line that doesn't split; this is for a profile written around it.
            if (!CommandLine.TryParse(line, out var argv, out var problem))
            {
                return WorkflowErrors.EffectFailed.With($"workflow.build.commands '{line}' {problem}");
            }

            commands.Add(new BuildCommand(line, argv));
        }

        return new CommandSet(commands, each);
    }

    private static TimeSpan DefaultEach { get; } = TimeSpan.FromHours(2);
}
