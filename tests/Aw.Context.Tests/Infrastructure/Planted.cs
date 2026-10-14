namespace Aw.Context.Tests.Infrastructure;

/// <summary>A secret the built-in rules redact (github-token), built at run time so no secret-shaped literal sits in the source.</summary>
internal static class Planted
{
    internal static string Secret { get; } = "gh" + "p_" + new string('Q', 36);
}
