using Aw.Sindri.Core.Workflow.Engine;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Where an agent Step runs (engine P8 D8): the agent is the APPROVED profile's 'workflow.agent.target', and the item and its repo checkout come from the instance's
/// stored 'fact:item' and the profile's 'repos' entry of that name. Read when the launch activity runs, so a retry sees the current approval.
/// </summary>
/// <param name="approved">The approved profile.</param>
/// <param name="contents">Reads the stored 'fact:item'.</param>
internal sealed class AgentTargetResolver(IApprovedProfile approved, IFactContents contents)
{
    private static readonly System.Text.Json.JsonSerializerOptions _options = AwJson.CreateOptions(WorkflowsJsonContext.Default);

    /// <summary>The target, or why there is none.</summary>
    /// <param name="item">The instance's stored 'fact:item'; null when it has none.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The target; SND-LAUNCH-003 when the profile names no agent, SND-LAUNCH-004 when the item or its repo can't be found.</returns>
    public async ValueTask<Result<AgentStepTarget>> Resolve(FactValue? item, CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        if (latest.Value?.Profile.Workflow?.Agent?.Target is not { } agent)
        {
            return SindriErrors.LaunchAgentMissing.With("the approved profile has no workflow.agent.target (fix: set it and approve the profile)");
        }

        var read = await ReadItem(item, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        return RepoOf(latest.Value.Profile, read.Value, agent);
    }

    private static Result<AgentStepTarget> RepoOf(SindriProfile profile, ItemFact item, AgentId agent)
    {
        var path = profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, item.Repo, StringComparison.Ordinal))?.Path;
        return path is null || !ItemId.TryFrom(item.Id, out var id)
            ? SindriErrors.LaunchRepoMissing.With($"the item's repo '{item.Repo}' is not in the approved profile's repos (fix: add it and approve the profile)")
            : new AgentStepTarget(id, agent, path);
    }

    private async ValueTask<Result<ItemFact>> ReadItem(FactValue? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return SindriErrors.LaunchRepoMissing.With("the instance has no fact:item, so there is no repo to run in");
        }

        var bytes = await contents.Read(item, cancellationToken).ConfigureAwait(false);
        if (bytes.IsError)
        {
            return bytes.Error;
        }

        var parsed = AwJson.Deserialize<ItemFact>(bytes.Value.Span, _options);
        return parsed.IsError || parsed.Value.Repo is not { Length: > 0 }
            ? SindriErrors.LaunchRepoMissing.With("the instance's fact:item names no repo")
            : parsed.Value;
    }
}
