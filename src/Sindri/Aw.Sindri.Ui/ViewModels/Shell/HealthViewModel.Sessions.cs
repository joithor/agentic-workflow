using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The session collector's counts from the last status (<see cref="HealthViewModel"/>, continued; U11a T5).</summary>
internal sealed partial class HealthViewModel
{
    /// <summary>The status' session counts; null before the first status and when the status serves none (before CO-3).</summary>
    internal SessionCountsView? Sessions { get; private set => Set(ref field, value); }
}
