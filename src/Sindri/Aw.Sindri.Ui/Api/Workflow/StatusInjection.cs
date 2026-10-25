namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>An injection line of a status.</summary>
/// <param name="Section">Section, as served.</param>
/// <param name="Input">Input, as served.</param>
/// <param name="Bytes">Size in bytes, as served.</param>
/// <param name="Hash">Hash, as served.</param>
/// <param name="Replaced">The earlier input whose section of the same name this one replaced; null when none.</param>
internal sealed record StatusInjection(
    string Section,
    string Input,
    int Bytes,
    string Hash,
    string? Replaced = null);
