namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// An input fact as the workflow input carries it (engine spec rev 8 §5.1, §6.4 'inputs'): the file name the human gave
/// ('spec.md'), which status shows, and the stored reference the API wrote before the start.
/// </summary>
/// <param name="Name">The file name the fact's content came from.</param>
/// <param name="Value">The stored content's reference and SHA-256.</param>
public sealed record InputFact(string Name, FactValue Value);
