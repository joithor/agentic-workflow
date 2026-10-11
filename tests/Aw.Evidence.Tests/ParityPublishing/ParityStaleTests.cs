using Aw.Evidence.Core;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Features.ParityStale;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.ParityPublishing;

public sealed class ParityStaleTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ParityStale_selects_pixel_diffs_not_kept_and_ignores_other_fields()
    {
        var files = new InMemoryRunFiles().With("/work/attachments.json", TestJson.Utf8(
            """[{"id":"a1","title":"Pixel diff: default (Figma | implementation | diff)","url":"https://x"},{"id":"a2","title":"Pixel diff: default (Figma | implementation | diff)"},{"id":"a3","title":"Screenshot"}]"""));

        var result = await new ParityStaleService(files, TestJson.Options)
            .Handle(new("/work/attachments.json", [AttachmentId.From("a2")]), CancellationToken.None);

        Assert.Equal(["a1"], result.Value.Stale.Select(static a => a.Id.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ParityStale_refuses_a_file_that_is_not_an_attachment_list()
    {
        var files = new InMemoryRunFiles().With("/work/attachments.json", TestJson.Utf8("""{"id":"a1"}"""));

        var result = await new ParityStaleService(files, TestJson.Options).Handle(new("/work/attachments.json", []), CancellationToken.None);

        Assert.Equal(EvidenceErrors.Invalid.Code, result.Error.Code);
    }
}
