using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeBrowserSessionContractTests : BrowserSessionContract
{
    protected override Task<IBrowserSession> OpenContractPageAsync()
    {
        var page = new FakePage { Path = "/" };
        _ = page.Texts.Add("Hospitals");
        _ = page.Texts.Add("Open");
        foreach (var id in (string[])["open", "search", "next"])
        {
            _ = page.TestIds.Add(id);
        }

        page.Candidates.Add(new Candidate(0, "textbox", "Search", TestId.From("search"), Text: null));
        page.Candidates.Add(new Candidate(1, "button", "Open", TestId.From("open"), "Open"));
        page.Candidates.Add(new Candidate(2, "a", "Next", TestId.From("next"), "Next"));
        page.OnClick["testId:open"] = static p => p.Texts.Add("Panel open");
        page.OnClick["candidate:1"] = static p => p.Texts.Add("Panel open");
        page.OnClick["testId:next"] = static p => p.Path = "/next";
        page.Boxes["testId:open"] = new Box(8, 40, 52, 21);
        var options = new BrowserSessionOptions(new ResolvedHost("http://localhost:3000", HostKind.Local), new ViewportSize(1280, 800), RecordDirectory: null, UserAgentOverride: null);
        return Task.FromResult<IBrowserSession>(new FakeBrowserSession(page, options));
    }
}
