#pragma warning disable MA0003, MA0127 // fixture records are positional; their literals are named by the record

namespace Aw.Sindri.Testing.Views;

/// <summary>A loop edge: the back edge a loop records, and the cap.</summary>
/// <param name="Name">The loop name.</param>
/// <param name="From">The state the back edge leaves.</param>
/// <param name="To">The state it returns to.</param>
/// <param name="Max">The cap.</param>
public sealed record ShapeLoop(string Name, string From, string To, int Max);
