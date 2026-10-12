using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Onyx;
using Aw.Context.Testing.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Onyx;

/// <summary>
/// One onyx source over a fake Onyx server, built the way the registered factory builds it. The shape a later
/// ContextSourceContract&lt;OnyxSource&gt; binding needs: create the source, the server it reads, and the logs.
/// </summary>
internal sealed class OnyxSourceHarness : IDisposable
{
    public const string Pointer = "env:ONYX_FIXTURE_KEY";
    public static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<string> _teams = new(StringComparer.Ordinal) { "SPL" };

    public OnyxSourceHarness(Uri url, string? token)
    {
        Settings = OnyxSettings.Create(url.ToString(), Pointer, FakeOnyxServer.PinnedVersion).Value;
        var parts = new OnyxSourceParts(Http, name => name == Pointer ? token : null, new FakeTimeProvider(Now), Log);
        Source = new OnyxSources(parts).Create(Settings, _teams);
    }

    public OnyxSettings Settings { get; }

    public IContextSource Source { get; }

    public CapturingLogger<OnyxClient> Log { get; } = new();

    public HttpClient Http { get; } = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });

    public static SourceQuery Query(params string[] terms) => new(terms, AsOf: null, Limit: 10, TimeSpan.FromSeconds(5));

    public void Dispose() => Http.Dispose();
}
