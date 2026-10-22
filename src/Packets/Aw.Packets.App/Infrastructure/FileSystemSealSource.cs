using Aw.Kernel;
using Aw.Packets.Core;
using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Infrastructure;

/// <summary>Reads a seal file from disk (a regular file; nothing else is read) and parses it with <see cref="SealReader"/>.</summary>
internal sealed class FileSystemSealSource : ISealSource
{
    public ValueTask<Result<PacketHash>> Read(string path, CancellationToken cancellationToken) => throw new NotSupportedException();
}
