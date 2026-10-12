namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// Where an approved hash stands against this build (spec §4.3 RCP-CAP-003), as the validator reads it from
/// <see cref="RecipeRegistryView"/>. It mirrors the engine's approval check, which the validator can't name without a
/// namespace cycle (the engine's decisions use the validator).
/// </summary>
public enum RegistryApproval
{
    /// <summary>Approved, not revoked, and its pins match the build.</summary>
    Holds = 1,

    /// <summary>The hash was revoked.</summary>
    Revoked = 2,

    /// <summary>A pinned block's descriptor hash differs from this build's.</summary>
    Drifted = 3,
}
