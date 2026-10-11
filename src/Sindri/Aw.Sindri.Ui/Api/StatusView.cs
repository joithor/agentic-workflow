namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/status: the same record the CLI's 'sindri status' prints.</summary>
/// <param name="Running">Whether the answering process holds the host lease.</param>
/// <param name="Pid">Its process id.</param>
/// <param name="Epoch">The lease epoch it holds.</param>
/// <param name="LeaseExpiresAt">When the grant lapses without a renewal.</param>
/// <param name="Profile">The approved and live profile.</param>
/// <param name="Services">Each scheduled service with its cadence and last run.</param>
internal sealed record StatusView(
    bool Running,
    int Pid,
    long Epoch,
    DateTimeOffset? LeaseExpiresAt,
    ProfileView Profile,
    IReadOnlyList<ServiceView> Services);
