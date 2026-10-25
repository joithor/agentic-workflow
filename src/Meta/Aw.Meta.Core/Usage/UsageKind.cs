namespace Aw.Meta.Core.Usage;

/// <summary>Which usage limit a lane hit.</summary>
public enum UsageKind
{
    /// <summary>The rolling session limit.</summary>
    Session = 1,

    /// <summary>The weekly limit.</summary>
    Weekly = 2,

    /// <summary>Any other limit (a model's, an unnamed one).</summary>
    Other = 3,
}
