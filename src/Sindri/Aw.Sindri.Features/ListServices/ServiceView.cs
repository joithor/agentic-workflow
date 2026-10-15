using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListServices;

/// <summary>One service in the list.</summary>
/// <param name="Name">The service.</param>
/// <param name="Every">Its cadence, as the profile writes it.</param>
/// <param name="Paused">True when paused (P6).</param>
/// <param name="LastRun">Its last run, or null before the first.</param>
/// <param name="NextSlot">The last slot plus the cadence, or null before the first run.</param>
public sealed record ServiceView(ServiceName Name, string Every, bool Paused, LastRun? LastRun, DateTimeOffset? NextSlot);
