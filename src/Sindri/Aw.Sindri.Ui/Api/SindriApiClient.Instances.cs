namespace Aw.Sindri.Ui.Api;

/// <summary>The instance-change stream (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public IAsyncEnumerable<InstanceStreamItem> InstanceChanges(Uri api, string? instance, CancellationToken cancellationToken) => throw new NotSupportedException();
}
