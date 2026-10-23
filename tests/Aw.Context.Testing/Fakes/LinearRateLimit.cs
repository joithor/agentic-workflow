namespace Aw.Context.Testing.Fakes;

/// <summary>How a fake Linear server rate limits.</summary>
public enum LinearRateLimit
{
    /// <summary>It does not.</summary>
    None,

    /// <summary>It answers HTTP 429.</summary>
    Http429,

    /// <summary>It answers HTTP 400 with a GraphQL error whose code is RATELIMITED.</summary>
    GraphQl,
}
