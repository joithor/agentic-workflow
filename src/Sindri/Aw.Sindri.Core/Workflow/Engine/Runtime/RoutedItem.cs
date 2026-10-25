namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The payload of the 'item.routed' event the router stub appends (spec §2, §12; engine P7 plan D8): the bridge reads its
/// 'outcome' ('startable' or 'notStartable') and the facts it records.
/// </summary>
/// <param name="Outcome">The routing outcome.</param>
/// <param name="Facts">The recorded facts by key: 'fact:stepSpec', the item's spec as a stored fact.</param>
public sealed record RoutedItem(string Outcome, IReadOnlyDictionary<string, FactValue> Facts);
