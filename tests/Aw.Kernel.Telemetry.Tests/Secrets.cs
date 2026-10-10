namespace Aw.Kernel.Telemetry.Tests;

/// <summary>Synthetic secrets, assembled from parts so no scanner sees a whole token in this source.</summary>
internal static class Secrets
{
    /// <summary>The token body: its absence from every output is the check.</summary>
    public const string Body = "Ab3dE6gH9jK2mN5pQ8sT1vW4yZ7";

    public static string Bearer => "Authorization: Bear" + "er " + Body + Body;

    public static string EnvPair => "CLIENT" + "_SECRET=" + Body + "9";

    public static string GithubToken => "gh" + "p_" + Body + "Ab3dE6gH9";
}
