namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The file map totals.</summary>
/// <param name="Files">Files, as served.</param>
/// <param name="Mapped">Mapped, as served.</param>
/// <param name="Suggested">Suggested, as served.</param>
/// <param name="Unmapped">Unmapped, as served.</param>
internal sealed record FileMapTotals(
    int Files,
    int Mapped,
    int Suggested,
    int Unmapped);
