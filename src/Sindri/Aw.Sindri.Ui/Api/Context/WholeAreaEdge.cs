namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The edges of one type from one area to another, counted.</summary>
/// <param name="From">The source area's key.</param>
/// <param name="To">The target area's key.</param>
/// <param name="Type">The edge type.</param>
/// <param name="Count">How many edges.</param>
/// <param name="Confirmed">How many of them are confirmed.</param>
internal sealed record WholeAreaEdge(string From, string To, string Type, int Count, int Confirmed);
