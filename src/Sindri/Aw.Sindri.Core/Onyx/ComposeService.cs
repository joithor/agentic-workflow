namespace Aw.Sindri.Core.Onyx;

/// <summary>One service of the pinned compose file, as Sindri reads it.</summary>
/// <param name="Name">The service name, such as api_server.</param>
/// <param name="Image">The service's `image:` exactly as written (still holding ${…} references), or null when it has none.</param>
/// <param name="HasProfiles">The service has a `profiles:` key, so compose starts it only when a profile is active.</param>
public sealed record ComposeService(string Name, string? Image, bool HasProfiles);
