namespace Aw.Sindri.Core.Onyx;

/// <summary>One container of the compose project, as `docker compose ps` reports it.</summary>
/// <param name="Service">The compose service name, such as nginx.</param>
/// <param name="State">running, exited, restarting, created and so on.</param>
/// <param name="Health">healthy, unhealthy or starting when the service has a health check, otherwise null.</param>
/// <param name="Image">The image the container runs, as Docker reports it (repository@digest for a pinned one).</param>
/// <param name="Publishers">The ports it publishes on the host (an exposed-only port isn't listed).</param>
public sealed record ComposeContainer(string Service, string State, string? Health, string Image, IReadOnlyList<ComposePublisher> Publishers);
