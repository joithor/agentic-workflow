using Aw.Kernel;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): a refusal outranks views that are behind.</summary>
public abstract partial class SindriApiContract
{
    // K5 (run 5, testing-2): the caller check comes before the views' lag. A read that carries a sequence while the views lag and the
    // caller is refused is the refusal, not SND-API-011 (which would send the app waiting for views that will never answer it).
    [Fact]
    public async Task A_refusal_outranks_views_that_are_behind_on_status_and_services()
    {
        var server = new FakeSindriApi { Lag = ViewsLag.Behind };
        var api = Reach(server);
        _ = await api.RebuildViews(_api, Token);
        server.Refusal = A.NoTokenRefusal;
        var expected = new Error(A.NoTokenRefusal.Code, ErrorKind.Forbidden, A.NoTokenRefusal.Detail);

        Expect.Equal(expected, (await api.Status(_api, Token)).Error);
        Expect.Equal(expected, (await api.Services(_api, Token)).Error);
    }
}
