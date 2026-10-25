using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Embedding;

[Trait("Lane", "unit")]
public sealed class FakeEmbedderContractTests : EmbedderContract
{
    protected override EmbeddingsSettings Settings { get; } = new(Enabled: true, CodeIndexSettings.DefaultEmbeddingsUrl, Model: null);

    protected override Task<IEmbedder> Arrange() => Task.FromResult<IEmbedder>(new FakeEmbedder());
}
