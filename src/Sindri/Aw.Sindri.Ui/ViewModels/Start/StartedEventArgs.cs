namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>An accepted start: the instance to open.</summary>
internal sealed class StartedEventArgs : EventArgs
{
    /// <summary>An accepted start.</summary>
    /// <param name="id">The instance id (WFI-…).</param>
    /// <param name="address">The address the frame is opened by.</param>
    /// <param name="existing">True when the attempt already existed.</param>
    /// <param name="finished">True when it had ended.</param>
    internal StartedEventArgs(string id, string address, bool existing, bool finished)
    {
        (Id, Address, Existing, Finished) = (id, address, existing, finished);
    }

    /// <summary>The instance id.</summary>
    public string Id { get; }

    /// <summary>The address.</summary>
    public string Address { get; }

    /// <summary>True when the attempt already existed.</summary>
    public bool Existing { get; }

    /// <summary>True when it had ended.</summary>
    public bool Finished { get; }
}
