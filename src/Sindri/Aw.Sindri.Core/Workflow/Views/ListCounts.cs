using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The head counts of the Workflows page over the root set, before the open filter, so every count comes from rows.</summary>
/// <param name="Open">Rows not terminal.</param>
/// <param name="NeedsYou">Parked, escalated or decision rows.</param>
/// <param name="WithYou">Rows a person took over.</param>
/// <param name="Running">Rows running.</param>
/// <param name="Done">Rows finished.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ListCounts(int Open, int NeedsYou, int WithYou, int Running, int Done);
