using Aw.Context.Core.Onyx;
using Aw.Context.Infrastructure;
using Aw.Context.Infrastructure.Onyx;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Onyx;

/// <summary>AddContextInfrastructure registers the factory that builds an onyx source per profile settings and teams.</summary>
public sealed class OnyxRegistrationTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_onyx_source_factory_resolves_and_builds_a_source_from_settings_and_teams()
    {
        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(OnyxSourceHarness.Now))
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddContextInfrastructure(static _ => null);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var settings = OnyxSettings.Create("http://127.0.0.1:3000", OnyxSourceHarness.Pointer, "v4.9.0").Value;

        var source = provider.GetRequiredService<OnyxSources>().Create(settings, new HashSet<string>(StringComparer.Ordinal) { "SPL" });

        Assert.Equal("onyx", source.Describe().Id.Value);
    }
}
