namespace Aw.Evidence.Core.Stack;

/// <summary>
/// What counts as seed data. Unconfigured, it matches only fictional example.com addresses, so a real database fails
/// closed (provenance unknown, evidence stays local).
/// </summary>
/// <param name="Domains">Email domains seed profiles use.</param>
/// <param name="EmailPattern">A LIKE pattern at least one seed profile matches.</param>
public sealed record SeedPolicy(IReadOnlyList<string> Domains, string EmailPattern)
{
    /// <summary>The fail-closed default: example.com, 'seed-admin-%@example.com'.</summary>
    public static SeedPolicy Default { get; } = new(["example.com"], "seed-admin-%@example.com");

    /// <summary>The policy from settings text (UI_EVIDENCE_SEED_DOMAINS, comma-separated; UI_EVIDENCE_SEED_EMAIL_PATTERN); blank keeps the default.</summary>
    /// <param name="domains">The domains setting.</param>
    /// <param name="emailPattern">The pattern setting.</param>
    /// <returns>The policy.</returns>
    public static SeedPolicy From(string? domains, string? emailPattern)
    {
        string[] parsed = [.. (domains ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
        return new(parsed.Length > 0 ? parsed : Default.Domains, string.IsNullOrWhiteSpace(emailPattern) ? Default.EmailPattern : emailPattern);
    }
}
