namespace Aw.Redaction.Tests;

/// <summary>One corpus input: what it is, the text, the rule that must name its hit, and a fragment of the secret
/// that must not survive.</summary>
public sealed record SecretCase(string Name, string Input, string Rule, string Fragment)
{
    public override string ToString() => Name;
}
