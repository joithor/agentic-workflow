namespace Aw.Sindri.Host.Commands;

/// <summary>'context sources sync' and 'context map build': start the job and follow it.</summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="follower">Follows the job a start answers.</param>
internal sealed class ContextJobVerbs(ControlPlaneClient client, Output output, JobFollower follower)
{
    internal Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        _ = (client, output, follower, args, interrupt, again);
        throw new NotSupportedException();
    }
}
