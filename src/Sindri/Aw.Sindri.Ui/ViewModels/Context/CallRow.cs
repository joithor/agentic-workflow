namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One edge of a callers or callees panel.</summary>
/// <param name="Name">The symbol at the other end.</param>
/// <param name="Path">Its file.</param>
/// <param name="Detail">How the index knows: "syntax · full", "graphify · partial".</param>
/// <param name="Depth">How many calls away.</param>
internal sealed record CallRow(string Name, string Path, string Detail, int Depth)
{
    /// <summary>The second line of the row: the file, and how the index knows it.</summary>
    public string Caption => Labels.Dots(Path, Detail);
}
