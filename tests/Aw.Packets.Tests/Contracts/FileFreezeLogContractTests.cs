using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Packets.App;
using Aw.Packets.App.Infrastructure;
using Aw.Packets.Core;
using Aw.Packets.Core.Model;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Contracts;

[Lane(TestLane.Integration)]
public sealed class FileFreezeLogContractTests : FreezeLogContract, IDisposable
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(PacketsJsonContext.Default, indented: true);
    private readonly TempDirectory _state = new();

    protected override IFreezeLog Create() => new FileFreezeLog(new PacketsSettings(Path.Combine(_state.Path, "packets")), _json);

    protected override IFreezeLog CreateUnwritable()
    {
        var file = Path.Combine(_state.Path, "not-a-directory");
        File.WriteAllText(file, "x");
        return new FileFreezeLog(new PacketsSettings(file), _json);
    }

    protected override bool CorruptRecord(IFreezeLog log)
    {
        var file = Directory.EnumerateFiles(Path.Combine(_state.Path, "packets"), "*", SearchOption.AllDirectories).Single();
        File.WriteAllText(file, File.ReadAllText(file).Replace("\"machine\"", "\"robot\"", StringComparison.Ordinal));
        return true;
    }

    protected override IReadOnlyList<FreezeRecord> ReadBack(IFreezeLog log)
    {
        var root = Path.Combine(_state.Path, "packets");
        return Directory.Exists(root)
            ? [.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
                .Select(f => AwJson.Deserialize<FreezeRecord>(File.ReadAllBytes(f), _json).Value)]
            : [];
    }

    public void Dispose() => _state.Dispose();
}
