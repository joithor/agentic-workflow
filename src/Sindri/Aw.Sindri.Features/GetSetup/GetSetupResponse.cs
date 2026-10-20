using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.GetSetup;

/// <summary>The setup list.</summary>
/// <param name="Steps">The six steps in their fixed order: repo, profile, recipes, mcp, map, onyx.</param>
/// <param name="AsOf">When the answer was read.</param>
public sealed record GetSetupResponse(IReadOnlyList<SetupStep> Steps, DateTimeOffset AsOf);
