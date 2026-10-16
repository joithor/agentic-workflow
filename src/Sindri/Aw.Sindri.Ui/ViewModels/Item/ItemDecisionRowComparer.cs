namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>Compares decision rows by value, options included (the record's own equality would compare the option list by reference).</summary>
internal sealed class ItemDecisionRowComparer : IEqualityComparer<ItemDecisionRow>
{
    internal static ItemDecisionRowComparer Instance { get; } = new();

    public bool Equals(ItemDecisionRow? x, ItemDecisionRow? y) =>
        ReferenceEquals(x, y) || (x is not null && y is not null && x.Id == y.Id && x.Question == y.Question && x.FiledBy == y.FiledBy && x.Options.SequenceEqual(y.Options));

    public int GetHashCode(ItemDecisionRow obj) => HashCode.Combine(obj?.Id);
}
