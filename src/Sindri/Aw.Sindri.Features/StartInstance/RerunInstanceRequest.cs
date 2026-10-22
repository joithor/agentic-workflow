using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.StartInstance;

/// <summary>
/// 'RerunInstance' (POST /workflow/instances/{instanceId}/rerun; desk): starts the same recipe again on the same item with the same input facts, as the next attempt,
/// from an instance that ended failed, unresolved or cancelled. The new instance carries <c language="text">again</c>, the old one's address.
/// </summary>
/// <param name="Instance">The instance to run again: its id or address.</param>
public sealed record RerunInstanceRequest(string Instance) : ICommand<StartInstanceResponse>;
