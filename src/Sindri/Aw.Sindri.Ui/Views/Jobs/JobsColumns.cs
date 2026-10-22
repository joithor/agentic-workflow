namespace Aw.Sindri.Ui.Views.Jobs;

/// <summary>The Jobs table's column widths: the header and every row share them (F3 jobs spec §2).</summary>
internal static class JobsColumns
{
    /// <summary>Job, Status, When, Started by, What happened, Rounds, Cancel.</summary>
    internal const string Wide = "1.6*,110,150,1.2*,130,56,120";

    /// <summary>Under 760 px: Job, Status, Rounds and Cancel.</summary>
    internal const string Compact = "1.6*,110,0,0,0,56,120";
}
