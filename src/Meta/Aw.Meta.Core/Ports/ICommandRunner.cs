using Aw.Kernel;
using Aw.Meta.Core.Commands;

namespace Aw.Meta.Core.Ports;

/// <summary>Runs external commands: git, gh, dotnet and the gate's steps.</summary>
public interface ICommandRunner
{
    /// <summary>Runs a command to completion or until its timeout.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the run and kills the command.</param>
    /// <returns>The result, or AW-META-009 when the program can't be started.</returns>
    public ValueTask<Result<CommandResult>> Run(CommandRequest request, CancellationToken cancellationToken);
}
