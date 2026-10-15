#pragma warning disable MA0003, MA0127 // fixture records are positional; their literals are named by the record
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Testing.Views;

/// <summary>A declared input.</summary>
/// <param name="Key">The fact key.</param>
/// <param name="Label">The label, null when the recipe declares none.</param>
/// <param name="Type">The fact type.</param>
/// <param name="Kind">The subject kind, if declared.</param>
public sealed record ShapeInput(string Key, string? Label, FactType Type, string? Kind);
