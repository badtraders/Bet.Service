using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TradingTerminal.Models.Bindables
{
    public class StorageBindableBase : BindableBase
    {
        private Dictionary<string, object> _storage = new Dictionary<string, object>();

        protected virtual bool SetValue<T>(T value, [CallerMemberName] string propertyName = null)
        {
            var storedValue = GetValue<T>(propertyName);

            if (EqualityComparer<T>.Default.Equals(storedValue, value)) return false;

            _storage[propertyName] = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        protected virtual T GetValue<T>([CallerMemberName] string propertyName = null)
        {
            return _storage.ContainsKey(propertyName) ? (T)_storage[propertyName] : default;
        }
    }
}
