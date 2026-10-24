namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A review with nothing to open was pressed: the person is taken to the instance's frame to find its fact.</summary>
/// <param name="address">The instance's address.</param>
/// <param name="fact">The fact's key.</param>
internal sealed class NeedsYouFactEventArgs(string address, string fact) : EventArgs
{
    /// <summary>The instance's address.</summary>
    public string Address { get; } = address;

    /// <summary>The fact's key.</summary>
    public string Fact { get; } = fact;
}
