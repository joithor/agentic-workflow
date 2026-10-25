using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>One concept as the first layer of the context map shows it: its name, its summary and its size.</summary>
internal sealed class ConceptTileViewModel
{
    private readonly Action<string> _open;

    internal ConceptTileViewModel(string id, string name, string summary, ConceptSize size, bool setByPerson, Action<string> open)
    {
        (Id, Name, Summary, _open) = (id, name, summary, open);
        Size = size;
        IsSetByPerson = setByPerson;
        SizeText = Labels.ContextConceptSize(size);
        AccessibleName = Labels.ContextConceptTileName(name, SizeText);
    }

    /// <summary>The concept's id: the value of the map read's concept parameter.</summary>
    public string Id { get; }

    /// <summary>The concept's name.</summary>
    public string Name { get; }

    /// <summary>The summary; empty when the server gave none.</summary>
    public string Summary { get; }

    /// <summary>Whether the summary has words.</summary>
    public bool HasSummary => Summary.Length > 0;

    /// <summary>What the concept holds.</summary>
    internal ConceptSize Size { get; }

    /// <summary>"4 features · 3 records · 2 decisions · 40 code files".</summary>
    public string SizeText { get; }

    /// <summary>Whether a person set this concept, not the model.</summary>
    public bool IsSetByPerson { get; }

    /// <summary>The words that say a person set it.</summary>
    public string SetByPersonText { get; } = Labels.ContextConceptSetByPerson;

    /// <summary>The accessible name.</summary>
    public string AccessibleName { get; }

    /// <summary>Opens the concept (click, Enter).</summary>
    public void Open() => _open(Id);
}
