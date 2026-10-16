namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The frame asks to open another instance (a child pointer, a child row).</summary>
/// <param name="address">The instance's address.</param>
/// <param name="id">Its opaque id when the frame knows it (so the read needs no lookup); null otherwise.</param>
internal sealed class FrameOpenEventArgs(string address, string? id) : EventArgs
{
    /// <summary>The instance's address.</summary>
    public string Address { get; } = address;

    /// <summary>Its id, or null.</summary>
    public string? Id { get; } = id;
}
