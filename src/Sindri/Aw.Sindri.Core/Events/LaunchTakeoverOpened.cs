using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'launch.takeover-opened' payload (issue 1004): a takeover tried to open the live session of the Step it took over, in the launcher.json viewer or the box's detected terminal. Key 'launch.takeover-opened:&lt;nonce&gt;'.</summary>
/// <param name="Launch">The live launch whose session it opened.</param>
/// <param name="Item">The item.</param>
/// <param name="Instance">The instance that was taken over.</param>
/// <param name="Viewer">The viewer, its outcome, the reason when nothing opened and the attach command.</param>
public sealed record LaunchTakeoverOpened([property: JsonPropertyName("launchId")] LaunchId Launch, ItemId Item, InstanceAddress Instance, TakeoverViewer Viewer);
