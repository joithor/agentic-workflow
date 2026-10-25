namespace Aw.Sindri.Ui.ViewModels.Components;

/// <summary>The component's own small input (the screens contract binds nothing else yet).</summary>
/// <param name="Address">The instance address, an opaque string (ITM-…/project/1 or …/item.4).</param>
/// <param name="Kind">The Step's kind.</param>
/// <param name="AttachReady">True once the command catalog says Attach is ready (API P10a).</param>
/// <param name="ConfirmText">The served Cancel confirm, when the server serves one.</param>
/// <param name="PendingReason">Why Attach is not ready, as the item serves it ("No live session to attach to"); null shows the catalog wait.</param>
internal sealed record AttachInput(string Address, AttachStepKind Kind, bool AttachReady, string? ConfirmText = null, string? PendingReason = null);
