using System.Collections.Generic;

namespace Streamx.Linq.SQL.EFCore.DSL {
    sealed class ScopedDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IDictionary<TKey, TValue> {
        private readonly IDictionary<TKey, TValue> _upper;
        private IList<TKey> _removedKeys = Collections.emptyList<TKey>();
        public ScopedDictionary(IDictionary<TKey, TValue> upper) {
            _upper = upper;
        }

        bool IDictionary<TKey, TValue>.ContainsKey(TKey key) => base.ContainsKey(key) || _upper.ContainsKey(key);

        int ICollection<KeyValuePair<TKey, TValue>>.Count => base.Count + _upper.Count;

        TValue IDictionary<TKey, TValue>.this[TKey key] {
            get => base.TryGetValue(key, out var value) ? value : _upper[key];
            set => base[key] = value;
        }

        bool IDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value) {
            return base.TryGetValue(key, out value) || (!_removedKeys.Contains(key) && _upper.TryGetValue(key, out value));
        }

        bool IDictionary<TKey, TValue>.Remove(TKey key) {
            if (base.Remove(key))
                return true;

            if (_upper.TryGetValue(key, out var _)) {
                if (_removedKeys.IsEmpty())
                    _removedKeys = new List<TKey>();
                _removedKeys.Add(key);
                return true;
            }

            return false;
        }
    }
}