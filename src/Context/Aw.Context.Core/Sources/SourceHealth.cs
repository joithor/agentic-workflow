namespace Aw.Context.Core.Sources;

/// <summary>A source's probe result. The detail names codes, counts and paths, never record text.</summary>
/// <param name="State">Its state.</param>
/// <param name="Detail">One line on what was checked.</param>
/// <param name="Code">The catalogue code of the problem, or null when ok.</param>
public sealed record SourceHealth(SourceState State, string Detail, string? Code);
