namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A protected verb's challenge; the app only shows the CLI.</summary>
/// <param name="Challenge">Challenge, as served.</param>
/// <param name="Action">Action, as served.</param>
/// <param name="ExpiresAt">ExpiresAt, as served.</param>
internal sealed record ReviewChallenge(
    string Challenge,
    string Action,
    DateTimeOffset ExpiresAt);
