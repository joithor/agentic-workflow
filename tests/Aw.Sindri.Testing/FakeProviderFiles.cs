using Aw.Sindri.Core.Mcp;

namespace Aw.Sindri.Testing;

/// <summary>The providers' MCP files in memory. Kept honest by <see cref="ProviderFilesContract"/>.</summary>
public sealed class FakeProviderFiles : IProviderFiles
{
    private readonly Dictionary<ProviderFile, (string? Text, string? Problem)> _files = [];

    /// <summary>Sets a file's text.</summary>
    /// <param name="file">Which file.</param>
    /// <param name="text">Its text.</param>
    /// <returns>These files.</returns>
    public FakeProviderFiles With(ProviderFile file, string text)
    {
        _files[file] = (text, null);
        return this;
    }

    /// <summary>Makes a file unreadable.</summary>
    /// <param name="file">Which file.</param>
    /// <returns>These files.</returns>
    public FakeProviderFiles Unreadable(ProviderFile file)
    {
        _files[file] = (null, "permission denied");
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<ProviderFileRead> Read(ProviderFile file, CancellationToken cancellationToken)
    {
        var (text, problem) = _files.TryGetValue(file, out var entry) ? entry : (null, null);
        return ValueTask.FromResult(new ProviderFileRead(file, ProviderFilesContract.Shown(file), text, problem));
    }
}
