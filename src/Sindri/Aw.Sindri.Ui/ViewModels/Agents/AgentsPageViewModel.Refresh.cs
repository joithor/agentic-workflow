namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The Agents page's reads (<see cref="AgentsPageViewModel"/>, continued; T1 fills it).</summary>
internal sealed partial class AgentsPageViewModel
{
    /// <summary>Reads the list at the API (on connect and on every event while the page is visible).</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) => throw new NotSupportedException("T1");
}
