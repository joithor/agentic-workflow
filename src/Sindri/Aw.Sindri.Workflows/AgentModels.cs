using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What a state's 'model' parameter asks for: a model id written in the recipe, or a profile key the engine can't resolve for an agent Step yet.</summary>
/// <param name="Literal">The model id the recipe wrote; null when it wrote none.</param>
/// <param name="Profile">The profile key of a '{ profile: … }' value; null when the value is not one.</param>
internal sealed record ModelAsk(string? Literal, string? Profile);

/// <summary>The 'model' parameter of an agent Step (issue 966): read like 'bundle', a literal id. A profile reference is refused at launch, since the activity has no profile-path resolver for it.</summary>
internal static class AgentModels
{
    /// <summary>Reads the Step's 'model' parameter.</summary>
    /// <param name="step">The state's Step.</param>
    /// <returns>The model it asks for.</returns>
    internal static ModelAsk Ask(StepUse step)
    {
        _ = step;
        throw new NotSupportedException();
    }

    /// <summary>Why a launch that asks for a profile key doesn't start.</summary>
    /// <param name="profile">The profile key.</param>
    /// <returns>The message, with its fix.</returns>
    internal static string Refusal(string profile)
    {
        _ = profile;
        throw new NotSupportedException();
    }
}
