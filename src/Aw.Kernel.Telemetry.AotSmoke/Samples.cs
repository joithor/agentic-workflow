namespace Aw.Kernel.Telemetry.AotSmoke;

/// <summary>Synthetic secrets, assembled from parts so no scanner sees a whole token in this source.</summary>
internal static class Samples
{
    public const string Body = "Ab3dE6gH9jK2mN5pQ8sT1vW4yZ7";

    public static string Bearer => "Authorization: Bear" + "er " + Body + Body;

    public static string GithubToken => "gh" + "p_" + Body + "Ab3dE6gH9";
}
