using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    public sealed class BusinessRuleContext
    {
        private readonly Dictionary<string, object> _items = new Dictionary<string, object>();

        public T Get<T>(string key) => (T)_items[key];
        public bool TryGet<T>(string key, out T value)
        {
            if (_items.TryGetValue(key, out object? val) && val is T typed)
            {
                value = typed;
                return true;
            }

            value = default!;
            return false;
        }

        public void Set<T>(string key, T value) => _items[key] = value!;
    }
}