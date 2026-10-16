#pragma warning disable MA0182, CA1822, MA0041, IDE0021, MA0003, CS0067, CS9113, IDE0060, CA1801 // red stub: the members throw until the green commit
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The instance frame (screens §4.2): one instance, read by its id.</summary>
internal sealed class InstanceFrameViewModel(ISindriApi api, IClipboard clipboard, TimeProvider clock) : ObservableObject
{
    /// <summary>Raised when the frame asks to open another instance.</summary>
    internal event EventHandler<FrameOpenEventArgs>? OpenRequested;

    /// <summary>Where the read stands.</summary>
    public FramePhase Phase => throw new NotSupportedException();

    /// <summary>The instance's address.</summary>
    public string Address => throw new NotSupportedException();

    /// <summary>The title as served.</summary>
    public string Title => throw new NotSupportedException();

    /// <summary>The recipe's id.</summary>
    public string Recipe => throw new NotSupportedException();

    /// <summary>The first twelve characters of the recipe's hash, as served.</summary>
    public string Hash12 => throw new NotSupportedException();

    /// <summary>"v12".</summary>
    public string Version => throw new NotSupportedException();

    /// <summary>The state word the status header serves.</summary>
    public string StateText => throw new NotSupportedException();

    /// <summary>The header's detail, or empty.</summary>
    public string Detail => throw new NotSupportedException();

    /// <summary>True when the header serves a detail.</summary>
    public bool HasDetail => throw new NotSupportedException();

    /// <summary>"since 09:14 (3h 12m)".</summary>
    public string SinceText => throw new NotSupportedException();

    /// <summary>The header's identity line.</summary>
    public string Identity => throw new NotSupportedException();

    /// <summary>The subject line; null before a read.</summary>
    public SubjectLineViewModel? Subject => throw new NotSupportedException();

    /// <summary>The states band; null before a read.</summary>
    public StatesBandViewModel? Band => throw new NotSupportedException();

    /// <summary>The expanded state; null before a read.</summary>
    public ExpandedStateViewModel? Expanded => throw new NotSupportedException();

    /// <summary>The sentence a refusal or an unreadable answer shows.</summary>
    public string ProblemText => throw new NotSupportedException();

    /// <summary>Back's words.</summary>
    public string BackText => throw new NotSupportedException();

    /// <summary>The note under the header (done, or gone); empty when none.</summary>
    public string Note => throw new NotSupportedException();

    /// <summary>Where focus lands.</summary>
    public FrameFocus InitialFocus => throw new NotSupportedException();

    /// <summary>Names the instance now, before any read.</summary>
    /// <param name="address">The address.</param>
    /// <param name="id">The id when known.</param>
    internal void Show(string address, string? id) => throw new NotSupportedException();

    /// <summary>Opens an instance: resolves the address when no id is known, then reads by id.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="address">The address.</param>
    /// <param name="id">The id when known.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the frame shows the answer.</returns>
    internal Task Open(Uri url, string address, string? id, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Re-reads the instance shown (on every event).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the frame shows the answer.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) => throw new NotSupportedException();
}
