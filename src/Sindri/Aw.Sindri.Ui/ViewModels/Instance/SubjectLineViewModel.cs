#pragma warning disable CA1822, MA0041, IDE0021, MA0003, CS0067, CS9113, IDE0060, CA1801 // red stub: the members throw until the green commit
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The frame's subject line: the served label, and Open in when the subject has a web link.</summary>
internal sealed class SubjectLineViewModel
{
    internal SubjectLineViewModel(SubjectRef subject, string address) => throw new NotSupportedException();

    /// <summary>The served label.</summary>
    public string Label => throw new NotSupportedException();

    /// <summary>The subject's kind as served.</summary>
    public string Kind => throw new NotSupportedException();

    /// <summary>The web link, or empty.</summary>
    public string Href => throw new NotSupportedException();

    /// <summary>True when the subject has a link the app may open.</summary>
    public bool HasHref => throw new NotSupportedException();

    /// <summary>The link's words.</summary>
    public string OpenInText => throw new NotSupportedException();

    /// <summary>The link's accessible name.</summary>
    public string OpenInName => throw new NotSupportedException();
}
