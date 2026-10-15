namespace Aw.Context.Testing.Fakes;

/// <summary>What the fake Onyx server knows about the one key it accepts, and the version it reports.</summary>
/// <param name="Token">The key.</param>
/// <param name="AccountType">Its account's type, as 'GET /api/me' reports it.</param>
/// <param name="Admin">Whether it holds the 'admin' permission.</param>
/// <param name="Version">What 'GET /api/version' reports.</param>
internal sealed record OnyxKey(string Token, string AccountType, bool Admin, string Version)
{
    /// <summary>Without the token: the generated ToString would print it.</summary>
    /// <returns>A description.</returns>
    public override string ToString() => $"OnyxKey {{ AccountType = {AccountType}, Admin = {Admin}, Version = {Version}, Token = (withheld) }}";
}
