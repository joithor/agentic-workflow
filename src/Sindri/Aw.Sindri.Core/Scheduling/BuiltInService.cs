using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Scheduling;

/// <summary>
/// A background service the build runs without a profile entry (engine spec §7.2 S1-D8), for example the workflow
/// engine's event bridge. Registered next to its <see cref="IScheduledService"/>; a profile entry of the same name sets
/// its cadence instead.
/// </summary>
/// <param name="Name">The service, as <see cref="IScheduledService.Identity"/> names it.</param>
/// <param name="Cadence">How often it runs when the profile doesn't say.</param>
public sealed record BuiltInService(ServiceName Name, Cadence Cadence);
