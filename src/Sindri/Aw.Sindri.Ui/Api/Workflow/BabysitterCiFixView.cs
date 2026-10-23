namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>CI repair attempts.</summary>
/// <param name="Attempts">Attempts made.</param>
/// <param name="Succeeded">Attempts that turned CI green.</param>
/// <param name="StillRed">Attempts that left CI red.</param>
/// <param name="Rate">The share that succeeded (0 to 1); null when there were no attempts.</param>
internal sealed record BabysitterCiFixView(int Attempts, int Succeeded, int StillRed, double? Rate);
