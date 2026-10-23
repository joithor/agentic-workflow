using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.QueryContext;

public sealed class LinearDegradeTests
{
    private static readonly SourceDescriptor _linear = new(SourceId.From("linear"), SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false);

    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-CONTEXT-071")]
    [InlineData("SND-CONTEXT-072")]
    public async Task A_linear_source_that_fails_degrades_the_query_and_names_its_code(string code)
    {
        var error = code == ContextErrors.LinearKeyRefused.Code ? ContextErrors.LinearKeyRefused.With("Linear refused the key") : ContextErrors.LinearUnexpectedReply.With("Linear answered with an unexpected shape");
        var linear = new FakeContextSource(_linear, []) { Failure = error };

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), linear).Query(new ContextQuery("orientation"), Engines.Human(), CancellationToken.None);

        var degraded = Assert.Single(result.Value.Degraded);
        Assert.Equal(("linear", ContextErrors.SourceUnavailable.Code), (degraded.Source.Value, degraded.Code));
        Assert.StartsWith(code + ":", degraded.Message, StringComparison.Ordinal);
        Assert.NotEmpty(result.Value.Sections.Single().Items);
    }
}
