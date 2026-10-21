using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Testing.Packets;

/// <summary>
/// The in-memory <see cref="IPacketFiles"/>: files put by directory and name, and, when given the in-memory run area, the files a Step wrote there
/// (read through their <see cref="InMemoryRunArea.PathFor"/>), under the same name rule as the disk adapter.
/// </summary>
/// <param name="area">The run area whose files are read by location, or null.</param>
public sealed class InMemoryPacketFiles(InMemoryRunArea? area = null) : IPacketFiles
{
    private readonly Dictionary<(string Directory, string Name), string> _files = [];

    /// <summary>Puts a file in a packet directory.</summary>
    /// <param name="directory">The directory.</param>
    /// <param name="name">The file's name.</param>
    /// <param name="text">The text.</param>
    /// <returns>The same instance.</returns>
    public InMemoryPacketFiles Put(string directory, string name, string text)
    {
        _files[(directory, name)] = text;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<string>> Read(string directory, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(name);
        cancellationToken.ThrowIfCancellationRequested();
        if (name.Contains('/', StringComparison.Ordinal) || name.StartsWith('.') || name.Length == 0)
        {
            return ValueTask.FromResult<Result<string>>(SindriErrors.InvalidInput.With($"'{name}' is not a packet file name"));
        }

        var text = _files.GetValueOrDefault((directory, name)) ?? area?.ReadLocation(directory + "/" + name);
        return ValueTask.FromResult<Result<string>>(text is null ? SindriErrors.ItemNotFound.With($"The packet in {directory} has no readable file {name}.") : text);
    }
}
