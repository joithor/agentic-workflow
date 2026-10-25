using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>What the frame read about an agent run: the launch, the item's launch block and the instance's events. Each is null when it could not be read.</summary>
/// <param name="Launch">The launch, or null.</param>
/// <param name="Item">The item, or null.</param>
/// <param name="Events">The instance's events (empty when not read).</param>
/// <param name="ItemUnreadable">True when the item was asked for and could not be read.</param>
/// <param name="SessionRef">The ref (provider/id) of the session row whose launch id is this run's launch, or null when none is known.</param>
internal sealed record AgentRunRead(LaunchView? Launch, ItemView? Item, IReadOnlyList<InstanceEventRow> Events, bool ItemUnreadable, string? SessionRef = null);
