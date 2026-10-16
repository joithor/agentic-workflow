namespace Aw.Sindri.Core.Events;

/// <summary>The subject a workflow.started row records (engine spec, P4c amendment 'When name is rendered'): what the instance is about, named once at the start.</summary>
/// <param name="Kind">The first input's subject kind.</param>
/// <param name="Key">The subject key.</param>
/// <param name="Label">The label: the rendered recipe name when the recipe has one.</param>
public sealed record EventSubject(string Kind, string Key, string Label);
