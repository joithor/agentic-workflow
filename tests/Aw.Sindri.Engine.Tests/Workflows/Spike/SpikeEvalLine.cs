namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>One trigger line of an eval case (spec §9.2): the Step report and the expected state.</summary>
/// <param name="Number">The line number in the file.</param>
/// <param name="Report">The trigger as a desk report.</param>
/// <param name="State">The expected state.</param>
/// <param name="Park">The expected park reason.</param>
internal sealed record SpikeEvalLine(int Number, SpikeReport Report, string State, string? Park);
