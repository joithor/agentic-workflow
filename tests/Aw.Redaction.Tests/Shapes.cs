namespace Aw.Redaction.Tests;

/// <summary>Synthetic secret shapes. Every value is assembled from parts, so neither this repo's scrubber nor push
/// protection sees a whole token in the source. None of them is a real credential.</summary>
internal static class Shapes
{
    // 27 mixed-case alphanumerics with digits: long enough for every rule, distinctive enough to grep for.
    public const string Body = "Ab3dE6gH9jK2mN5pQ8sT1vW4yZ7";

    public const string Tail = "Q8sT1vW4";

    public static string Join(params string[] parts) => string.Concat(parts);

    public static string Aws(string prefix) => Join(prefix, "ABCDEFGH", "IJKLMNOP");

    public static string Pem(string end) => Join(
        "-----BEGIN RSA PRIV", "ATE KEY-----\n", Body, Body, "\n", Body, "Ab3d\n", end);
}
