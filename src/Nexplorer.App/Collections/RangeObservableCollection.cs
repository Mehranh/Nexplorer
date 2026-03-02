using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Nexplorer.App.Collections;

/// <summary>Allows adding a range of items with a single CollectionChanged Reset notification,
/// avoiding O(n) notifications when populating large lists.</summary>
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items)
            Items.Add(item);

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
