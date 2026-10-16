using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

/// <summary>What the steps of one `onyx up` run share: the pins, the paths, the chosen port, the server's platform, and the stamps of the steps done so far.</summary>
/// <param name="Pins">The lock.</param>
/// <param name="Paths">Where the files live.</param>
/// <param name="Port">The loopback port.</param>
/// <param name="Platform">The Docker server's platform.</param>
internal sealed record OnyxUpContext(OnyxPins Pins, OnyxPaths Paths, int Port, string Platform)
{
    /// <summary>Gets the stamp of each step done in this run or recorded and still right, by step name.</summary>
    public Dictionary<string, string> Done { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the release the running Onyx reported once the start step finished.</summary>
    public string? Version { get; set; }

    /// <summary>Gets the compose project the docker operations act on.</summary>
    public ComposeProject Project => Paths.Project;

    /// <summary>Gets the settings the .env and the override are rendered with, for a given key.</summary>
    /// <param name="authKey">The USER_AUTH_SECRET value.</param>
    /// <returns>The settings.</returns>
    public EnvSettings Settings(string authKey) => new(Pins.Version, Port, authKey);
}
