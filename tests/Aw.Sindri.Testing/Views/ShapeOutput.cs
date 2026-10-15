#pragma warning disable MA0003, MA0127 // fixture records are positional; their literals are named by the record
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Testing.Views;

/// <summary>A declared output.</summary>
/// <param name="Key">The fact key.</param>
/// <param name="Label">The label.</param>
/// <param name="Type">The fact type.</param>
/// <param name="From">The state that produces it.</param>
public sealed record ShapeOutput(string Key, string Label, FactType Type, string From);
