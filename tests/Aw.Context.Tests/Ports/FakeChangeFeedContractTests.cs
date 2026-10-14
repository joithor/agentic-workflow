using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeChangeFeedContractTests : ChangeFeedContract<FakeChangeFeed>
{
    protected override FakeChangeFeed Create() => new([.. Enumerable.Range(1, Count).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).Select(i => new SourceRef(RepoDocs.Id,
        ExternalId.From($"split-panes/docs/{i}.md#L1-L2"), SourceVersion.From("v1"), Permalink: null, RepoDocs.Key(ContextRepoName.From("split-panes"), $"docs/{i}.md")))]);
}
