namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/queue (API spec §3.1 GetQueue, P3): every group in its fixed order, empty ones included (E-18).</summary>
/// <param name="Groups">The groups in the API's order.</param>
/// <param name="Empty">The view's EmptyText sentence for its state (empty, rebuilding or stalled), or null.</param>
/// <param name="AsOf">How fresh the view is.</param>
internal sealed record QueueView(IReadOnlyList<QueueGroupView> Groups, string? Empty, AsOfView AsOf);
