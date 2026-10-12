using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListServices;

/// <summary>The services list (API spec §3.1).</summary>
/// <param name="Services">Each approved service, by name.</param>
/// <param name="Empty">The EmptyText sentence for the list's state, or null when it has rows and the views are fresh.</param>
/// <param name="AsOf">How fresh the answer is.</param>
public sealed record ListServicesResponse(IReadOnlyList<ServiceView> Services, string? Empty, AsOf AsOf);
