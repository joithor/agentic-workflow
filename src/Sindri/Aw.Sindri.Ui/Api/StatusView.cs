namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/status: the same record the CLI's 'sindri status' prints.</summary>
/// <param name="Running">Whether the answering process holds the host lease.</param>
/// <param name="Pid">Its process id.</param>
/// <param name="Epoch">The lease epoch it holds.</param>
/// <param name="LeaseExpiresAt">When the grant lapses without a renewal.</param>
/// <param name="Profile">The approved and live profile.</param>
/// <param name="Services">Each scheduled service with its cadence and last run.</param>
/// <param name="ApiVersion">The API version Sindri speaks (API P1); absent before P1, which is no mismatch.</param>
/// <param name="Views">The views' freshness (API P2 ViewsStatus, §2.3; its stall fields aren't read); null or absent when unknown.</param>
/// <param name="Health">The health word and problems (UI spec E-14, API P2); null or absent when unknown.</param>
/// <param name="Sessions">The session collector's counts (collector spec CA-8): live, asking and failed sources; absent before CO-3.</param>
internal sealed record StatusView(
    bool Running,
    int Pid,
    long Epoch,
    DateTimeOffset? LeaseExpiresAt,
    ProfileView Profile,
    IReadOnlyList<ServiceView> Services,
    int? ApiVersion = null,
    AsOfView? Views = null,
    HealthView? Health = null,
    SessionCountsView? Sessions = null);
