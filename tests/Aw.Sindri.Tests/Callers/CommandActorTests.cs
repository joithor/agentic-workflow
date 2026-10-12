using System.Net;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Callers;

/// <summary>The events a command records carry the HTTP caller's actor, composed by the host (API spec §2.1, C6).</summary>
public sealed class CommandActorTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_command_records_its_events_as_the_callers_actor()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "profile"));
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "profile", "profile.yaml"), "schemaVersion: 1\nuser: thor\n", ct);
        var token = new HumanToken("token-of-this-start");
        await using var app = await CallerHost.Start(n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, token);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };
        var hash = (await app.Services.GetRequiredService<IProfileSource>().Load(ct)).Value.Hash;

        var approve = await http.Ask(Post("/sindri/profile/approve", $$"""{"hash":"{{hash.Value}}","confirmed":true}""", Human(token.Value)));

        var approved = (await app.Services.GetRequiredService<ILedgerStore>().Latest(EventTypes.ProfileApproved, ct)).Value;
        Assert.Equal(HttpStatusCode.OK, approve.Status);
        Assert.Equal("human:cli", approved?.Envelope.Actor);
        await app.StopAsync(ct);
    }
}
