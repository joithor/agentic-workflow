namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One output a desk task declares.</summary>
/// <param name="Key">Key, as served.</param>
/// <param name="Type">Type, as served.</param>
/// <param name="Required">Required, as served.</param>
internal sealed record DeskOutput(
    string Key,
    FactType Type,
    bool Required);
