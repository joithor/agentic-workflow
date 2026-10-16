using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>A frame as a delta shows it.</summary>
/// <param name="State">The state.</param>
/// <param name="Entry">The entry number.</param>
/// <param name="Run">The open run's token.</param>
/// <param name="Child">The child the frame waits on.</param>
/// <param name="Runtime">Who runs the open run (desk or agent); null when no run is open. A row from before this field with an open run reads as desk (the only runtime before the agent runtime, P8).</param>
public sealed record FrameSummary(
    string State,
    int Entry,
    string? Run,
    string? Child,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] StepRuntime? Runtime = null);
