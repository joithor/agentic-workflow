using Aw.Context.Core.Eval.Backtest;


namespace Aw.Context.Features.RunBacktest;

/// <summary>The run's adjudication spend: the one token cap (1.5M, spec §9.4) shared by every sample and arm.</summary>
/// <param name="Enabled">True when 'context.eval.adjudicator' is on.</param>
internal sealed class Spend(bool Enabled)
{
    /// <summary>True when the adjudicator is on.</summary>
    public bool Enabled { get; } = Enabled;

    /// <summary>Tokens spent so far.</summary>
    public int Total { get; private set; }

    /// <summary>True once a call was refused for the cap.</summary>
    public bool Capped { get; private set; }

    /// <summary>The code of the first model error, or null: after it the run stops asking the model.</summary>
    public string? Error { get; private set; }

    /// <summary>True when no further call is made: the cap or a model error stopped adjudication.</summary>
    public bool Stopped => Capped || Error is not null;

    /// <summary>Tokens per arm.</summary>
    public Dictionary<Arm, int> ByArm { get; } = [];

    /// <summary>Records one adjudication.</summary>
    /// <param name="arm">The arm it was for.</param>
    /// <param name="result">What happened.</param>
    public void Add(Arm arm, Adjudication result)
    {
        Total += result.Tokens;
        ByArm[arm] = ByArm.GetValueOrDefault(arm) + result.Tokens;
        Capped |= result.Capped;
        Error ??= result.Error;
    }
}
