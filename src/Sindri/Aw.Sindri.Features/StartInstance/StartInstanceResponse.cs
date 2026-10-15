using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.StartInstance;

/// <summary>What 'workflow start' did (engine spec §5.1, §6.3).</summary>
/// <param name="Instance">The instance, as 'GetInstance' shows it.</param>
/// <param name="Existing">Whether '(recipe, item, attempt)' already had it, so nothing was started.</param>
/// <param name="Finished">Whether that existing attempt has ended (the CLI adds 'start a new one with --attempt next').</param>
public sealed record StartInstanceResponse(InstanceView Instance, bool Existing, bool Finished);
