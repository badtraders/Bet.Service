using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace Trader.Extensions
{
    public class ObservableCollectionExtension<T> : ObservableCollection<T>
    {

        public enum ObservableCollectionExtensionType
        {
            List,
            Reversed
        }
        public int Capacity { get; private set; }
        public ObservableCollectionExtensionType Type { get; private set; }

        public ObservableCollectionExtension(ObservableCollectionExtensionType type = ObservableCollectionExtensionType.List, int capacity = 0) : base()
        {
            Capacity = capacity;
            Type = type;
        }

        public void ReplaceOrAdd(T newItem, Func<T, bool> findExisting, Action<T> beforeReplacementAction = null)
        {
            var existing = this.FirstOrDefaultWithIndex(findExisting);
            if (existing.index == -1)
            {
                switch (Type)
                {
                    case ObservableCollectionExtensionType.List:
                        Add(newItem);
                        break;
                    case ObservableCollectionExtensionType.Reversed:
                        Insert(0, newItem);
                        break;
                    default:
                        break;
                }

                if (Capacity > 0 && Count > Capacity)
                {
                    switch (Type)
                    {
                        case ObservableCollectionExtensionType.List:
                            RemoveAt(0);
                            break;
                        case ObservableCollectionExtensionType.Reversed:
                            RemoveAt(Count - 1);
                            break;
                        default:
                            break;
                    }
                }
            }
            else
            {
                beforeReplacementAction?.Invoke(existing.item);
                Items[existing.index] = newItem;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItem, existing, existing.index));
            }

        }

    }
}
