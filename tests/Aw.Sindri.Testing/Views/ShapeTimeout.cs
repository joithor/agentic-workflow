#pragma warning disable MA0003, MA0127 // fixture records are positional; their literals are named by the record
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Testing.Views;

/// <summary>A state's declared timeout: how long, and what it does when it fires (WF 3.4).</summary>
/// <param name="After">The declared duration.</param>
/// <param name="Then">The action.</param>
public sealed record ShapeTimeout(TimeSpan After, TimeoutAction Then);
