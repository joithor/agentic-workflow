using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Infrastructure.Embedding;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Embedding;

[Trait("Lane", "unit")]
public sealed class OllamaEmbedderContractTests : EmbedderContract
{
    protected override EmbeddingsSettings Settings { get; } = new(Enabled: true, CodeIndexSettings.DefaultEmbeddingsUrl, Model: null);

    protected override Task<IEmbedder> Arrange()
    {
        var server = new FakeOllamaServer();
        server.Models["some-model:2"] = new string('c', 64);
        return Task.FromResult<IEmbedder>(new OllamaEmbedder(new ModelPin(FakeOllamaServer.Model, "sha256:" + FakeOllamaServer.Digest), server));
    }
}
