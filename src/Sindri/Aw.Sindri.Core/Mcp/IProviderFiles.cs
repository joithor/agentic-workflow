namespace Aw.Sindri.Core.Mcp;

/// <summary>Reads the providers' user-wide MCP configuration files, from the home the environment names.</summary>
public interface IProviderFiles
{
    /// <summary>Reads <paramref name="file"/>.</summary>
    /// <param name="file">Which provider's file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file as read; a missing file has no text and no problem.</returns>
    public ValueTask<ProviderFileRead> Read(ProviderFile file, CancellationToken cancellationToken);
}
