using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Profile;

/// <summary>The checks on 'workflow.agent.models', 'pin', 'cap' and 'pick' (model-pick spec §8).</summary>
public static class ModelKeyRules
{
    /// <summary>The checks the profile alone settles: the id patterns, a non-empty model list and 'pick' being on or off.</summary>
    /// <param name="agent">The 'workflow.agent' block, or null.</param>
    /// <returns>SND-PROFILE-002 naming the key path, or null.</returns>
    public static Error? Shape(WorkflowAgentSettings? agent) => throw new NotSupportedException();

    /// <summary>The checks that need the box: agent ids against the catalog and pin states against the recipes, plus 'default' being in 'list'.</summary>
    /// <param name="agent">The 'workflow.agent' block, or null.</param>
    /// <param name="agentIds">The catalog's agent ids, or null to skip the agent-id check.</param>
    /// <param name="states">The states of the embedded and approved recipes, or null to skip the state check.</param>
    /// <returns>SND-MODEL-001 with the nearest id, or null.</returns>
    public static Error? Check(WorkflowAgentSettings? agent, IReadOnlyCollection<AgentId>? agentIds, IReadOnlyList<RecipeStateRef>? states) => throw new NotSupportedException();

    /// <summary>What each pin and cap matches, one line each: 'pin claude-code/questions = haiku: matches workflow.scoping state questions, workflow.design state questions'.</summary>
    /// <param name="agent">The 'workflow.agent' block, or null.</param>
    /// <param name="states">The states of the embedded and approved recipes.</param>
    /// <returns>The lines, pins first then caps, each in key order.</returns>
    public static IReadOnlyList<string> Matches(WorkflowAgentSettings? agent, IReadOnlyList<RecipeStateRef> states) => throw new NotSupportedException();
}
