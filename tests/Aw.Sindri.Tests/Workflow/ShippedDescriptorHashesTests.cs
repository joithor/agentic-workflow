using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>
/// No shipped descriptor's hash moves when a descriptor field is added (PB spec W23): a new field is written only when
/// set, so every approval that pins a descriptor still holds. Re-pin only when a shipped block's behaviour changes on purpose.
/// </summary>
public sealed class ShippedDescriptorHashesTests
{
    private const string Pinned = "890ce30fb59a00cb43016855e21c8de1c76204a78731c21fa395bb327689ed93";

    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-descriptor-hashes-test");

    [Fact, Trait("Lane", "unit")]
    public void Every_shipped_descriptor_hash_is_unchanged()
    {
        using var provider = Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? _stateRoot : null, new FakeTimeProvider())
            .BuildServiceProvider();
        var lines = provider.GetRequiredService<BlockCatalog>().Blocks
            .OrderBy(b => b.Id.Value, StringComparer.Ordinal)
            .Select(b => $"{b.Id.Value} {PinRules.Hash(b, SliceHarness.Json).Value}");

        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))));

        Assert.Equal(Pinned, digest);
    }
}
