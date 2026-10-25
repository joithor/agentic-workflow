namespace Aw.Judge.Core.Labels;

/// <summary>Who gave an eval item a label. An item has at most one label per source; wire names in <see cref="LabelSources"/>.</summary>
public enum LabelSource
{
    /// <summary>'outcome': what the user did next (the next real prompt, or whether the edited lines survived).</summary>
    Outcome = 1,

    /// <summary>'adjudicator': a stronger model's majority answer ('judge adjudicate').</summary>
    Adjudicator = 2,

    /// <summary>'override': a label set by hand ('judge label set'); beats the other two when the labels are read together.</summary>
    Override = 3,
}
