#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823 // red stub: removed in the green commit
namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>One node of a payload's tree: a key, its scalar value or a count, and its children.</summary>
internal sealed class PayloadNodeViewModel
{
    /// <summary>The key, or the index of an array item.</summary>
    public string Key => throw new NotSupportedException();

    /// <summary>The node's words: "key: value" for a scalar, "key" and a count for a container.</summary>
    public string Text => throw new NotSupportedException();

    /// <summary>The children of an object or an array.</summary>
    public IReadOnlyList<PayloadNodeViewModel> Children => throw new NotSupportedException();
}
