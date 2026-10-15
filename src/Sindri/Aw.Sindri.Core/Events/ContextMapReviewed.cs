namespace Aw.Sindri.Core.Events;

/// <summary>A review decision was recorded (context spec §3 'context.map.reviewed'): what the app refreshes its queue on.</summary>
/// <param name="Verb">The decision: 'accept', 'reject', 'rename', 'move', 'accept-deterministic', 'undo', 'drop', 'repoint', 'apply-file', 'merge', 'add' or 'delete'.</param>
/// <param name="Rows">The override rows it wrote.</param>
/// <param name="Undecided">The review items still left.</param>
public sealed record ContextMapReviewed(string Verb, int Rows, int Undecided);
