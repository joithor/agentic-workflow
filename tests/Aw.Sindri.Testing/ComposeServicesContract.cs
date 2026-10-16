using Aw.Sindri.Core.Onyx;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IComposeServices"/> honours, over the real v4.9.0 compose file (a fixture) and text that isn't a compose file.</summary>
public abstract class ComposeServicesContract
{
    private const string Compose = "docker_compose/docker-compose.yml";

    /// <summary>Gets the 11 services of the real v4.9.0 compose, with each `image:` as written and whether the service has `profiles:` (only minio does; Sindri's override adds profiles to indexing_model_server and code-interpreter).</summary>
    protected static IReadOnlyList<ComposeService> Expected { get; } =
    [
        new("api_server", "${ONYX_BACKEND_IMAGE:-onyxdotapp/onyx-backend:${IMAGE_TAG:-latest}}", HasProfiles: false),
        new("background", "${ONYX_BACKEND_IMAGE:-onyxdotapp/onyx-backend:${IMAGE_TAG:-latest}}", HasProfiles: false),
        new("web_server", "${ONYX_WEB_SERVER_IMAGE:-onyxdotapp/onyx-web-server:${IMAGE_TAG:-latest}}", HasProfiles: false),
        new("inference_model_server", "${ONYX_MODEL_SERVER_IMAGE:-onyxdotapp/onyx-model-server:${IMAGE_TAG:-latest}}", HasProfiles: false),
        new("indexing_model_server", "${ONYX_MODEL_SERVER_IMAGE:-onyxdotapp/onyx-model-server:${IMAGE_TAG:-latest}}", HasProfiles: false),
        new("relational_db", "${BASE_IMAGE_REGISTRY:-docker.io}/library/postgres:15.2-alpine", HasProfiles: false),
        new("opensearch", "${BASE_IMAGE_REGISTRY:-docker.io}/opensearchproject/opensearch:3.6.0", HasProfiles: false),
        new("nginx", "${BASE_IMAGE_REGISTRY:-docker.io}/library/nginx:1.25.5-alpine", HasProfiles: false),
        new("cache", "${BASE_IMAGE_REGISTRY:-docker.io}/library/redis:7.4-alpine", HasProfiles: false),
        new("minio", "${BASE_IMAGE_REGISTRY:-docker.io}/onyxdotapp/minio:RELEASE.2025-07-23T15-54-02Z-cpuv1@sha256:7330be2e7320a7a699b36f72bd06a94d8a21ec9f6f397345d9c38fc6751141f5", HasProfiles: true),
        new("code-interpreter", "onyxdotapp/code-interpreter:${CODE_INTERPRETER_IMAGE_TAG:-0.4.8}", HasProfiles: false),
    ];

    /// <summary>The reader over compose text. A scripted fake answers <see cref="Expected"/> for <paramref name="realCompose"/> and nothing else; the real one reads whatever it is given.</summary>
    /// <param name="realCompose">The real compose file's text.</param>
    /// <returns>The reader.</returns>
    protected abstract IComposeServices Create(string realCompose);

    /// <summary>The real compose file reads as its 11 services.</summary>
    [Fact]
    public void Reads_the_services_of_the_real_v4_9_0_compose()
    {
        var text = OnyxFixtures.ReadText(Compose);

        var services = Create(text).Read(text).Value;

        Expect.Sequence(Expected.Select(static s => s.Name).Order(StringComparer.Ordinal), services.Select(static s => s.Name).Order(StringComparer.Ordinal));
        foreach (var expected in Expected)
        {
            Expect.Equal(expected, services.Single(s => s.Name == expected.Name));
        }
    }

    /// <summary>Text that isn't YAML is SND-ONYX-004.</summary>
    [Fact]
    public void Text_that_is_not_yaml_is_004()
        => Expect.Equal("SND-ONYX-004", Create(OnyxFixtures.ReadText(Compose)).Read("services: [unclosed").Error.Code);

    /// <summary>YAML with no services is SND-ONYX-004.</summary>
    [Fact]
    public void A_compose_without_services_is_004()
        => Expect.Equal("SND-ONYX-004", Create(OnyxFixtures.ReadText(Compose)).Read("name: onyx\n").Error.Code);
}
