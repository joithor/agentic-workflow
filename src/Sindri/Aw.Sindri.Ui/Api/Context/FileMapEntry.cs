namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One child of the listed path.</summary>
/// <param name="Name">Name, as served.</param>
/// <param name="Path">Path, as served.</param>
/// <param name="Type">Type, as served.</param>
/// <param name="Status">Status, as served.</param>
/// <param name="Files">Files, as served.</param>
/// <param name="Features">Features, as served.</param>
/// <param name="Intent">Intent, as served.</param>
internal sealed record FileMapEntry(
    string Name,
    string Path,
    string Type,
    string Status,
    int Files,
    IReadOnlyList<FileFeature> Features,
    FileIntent? Intent);
