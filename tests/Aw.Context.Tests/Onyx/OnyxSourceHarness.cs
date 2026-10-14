using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Onyx;
using Aw.Context.Testing.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Onyx;

/// <summary>
/// One onyx source over a fake Onyx server, built the way the registered factory builds it: the source, the server it
/// reads, and the logs (OnyxSourceContractTests binds ContextSourceContract to it).
/// </summary>
internal sealed class OnyxSourceHarness : IDisposable
{
    public const string Pointer = "env:ONYX_FIXTURE_KEY";
    public static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<string> _teams = new(StringComparer.Ordinal) { "SPL" };

    public OnyxSourceHarness(Uri url, string? token)
        : this(url, name => name == Pointer ? token : null)
    {
    }

    /// <summary>A harness whose environment function is the caller's (to count or change what the pointer resolves to).</summary>
    public OnyxSourceHarness(Uri url, Func<string, string?> environment)
    {
        Settings = OnyxSettings.Create(url.ToString(), Pointer, FakeOnyxServer.PinnedVersion).Value;
        Sources = new OnyxSources(new OnyxSourceParts(Http, environment, new FakeTimeProvider(Now), Log) { CallDeadline = Patient });
        Source = Sources.Create(Settings, _teams);
    }

    /// <summary>The factory the source came from (one source per query in production).</summary>
    public OnyxSources Sources { get; }

    public static IReadOnlySet<string> Teams => _teams;

    public OnyxSettings Settings { get; }

    public IContextSource Source { get; }

    public CapturingLogger<OnyxClient> Log { get; } = new();

    public HttpClient Http { get; } = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });

    /// <summary>
    /// The deadline every call gets unless a test exercises the deadline itself: long enough that a loaded machine can't
    /// turn an answer into SND-CONTEXT-010 (OnyxVersionRequestTests and OnyxSourceFailureTests pin the expiry).
    /// </summary>
    public static readonly TimeSpan Patient = TimeSpan.FromSeconds(30);

    public static SourceQuery Query(params string[] terms) => new(terms, AsOf: null, Limit: 10, Patient);

    public void Dispose() => Http.Dispose();
}
