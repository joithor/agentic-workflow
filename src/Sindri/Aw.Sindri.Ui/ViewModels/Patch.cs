using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// Keeps a bound list's items in place across a re-read (UI spec §3.3: "a refresh never moves focus"). The list stays the
/// same object and only what differs is removed, inserted or moved, so the controls built for the items that remain are
/// not rebuilt and a control with focus keeps it.
/// </summary>
internal static class Patch
{
    /// <summary>Makes <paramref name="list"/> hold <paramref name="target"/>, in its order, touching only the differences (items compare by reference).</summary>
    /// <param name="list">The bound list.</param>
    /// <param name="target">What it should hold; instances already in the list are the ones to keep.</param>
    /// <typeparam name="T">The item type.</typeparam>
    internal static void To<T>(ObservableCollection<T> list, IReadOnlyList<T> target)
        where T : class
    {
        var keep = new HashSet<T>(target, ReferenceEqualityComparer.Instance);
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(list[i]))
            {
                list.RemoveAt(i);
            }
        }

        for (var i = 0; i < target.Count; i++)
        {
            if (i < list.Count && ReferenceEquals(list[i], target[i]))
            {
                continue;
            }

            var at = IndexOf(list, target[i], i);
            if (at >= 0)
            {
                list.Move(at, i);
            }
            else
            {
                list.Insert(i, target[i]);
            }
        }
    }

    private static int IndexOf<T>(ObservableCollection<T> list, T item, int from)
        where T : class
    {
        for (var i = from; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], item))
            {
                return i;
            }
        }

        return -1;
    }
}
