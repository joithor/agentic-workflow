namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The part of the started instance the form reads: its opaque id and its address. The rest of the engine's view is the frame's to read.</summary>
/// <param name="Id">The instance id (WFI-…).</param>
/// <param name="Address">The address ('T-1/step/1').</param>
internal sealed record StartedInstanceView(string Id, string Address);
