namespace Aw.Sindri.Ui.Navigation;

/// <summary>What a deep link opens (UI spec §3.3 "Deep links inside the app").</summary>
internal enum DeepLinkKind
{
    /// <summary>'home/needs-you': Home with its Needs you panel expanded.</summary>
    HomeNeedsYou,

    /// <summary>'needs-you/&lt;rowId&gt;': a needs-you row's detail.</summary>
    NeedsYou,

    /// <summary>'item/&lt;ItemId&gt;': an item's detail.</summary>
    Item,

    /// <summary>'decision/&lt;DecisionId&gt;': a decision's detail.</summary>
    Decision,

    /// <summary>'service/&lt;name&gt;': a service's detail.</summary>
    Service,

    /// <summary>'job/&lt;JobId&gt;': a job's detail.</summary>
    Job,
}
