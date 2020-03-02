using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Streamx.Linq.SQL.EFCore.DSL {
    static class Collections {
        public static bool IsEmpty<T>(this ICollection<T> l) => l.Count == 0;
        public static T get<T>(this IList<T> l, int index) => l[index];
        public static void set<T>(this IList<T> l, int index, T value) => l[index] = value;
        public static int size<T>(this ICollection<T> l) => l.Count;

        public static TValue get<TKey, TValue>(this IDictionary<TKey, TValue> map, TKey key) where TValue : class =>
            key == null ? null : map.TryGetValue(key, out TValue value) ? value : null;

        public static void put<TKey, TValue>(this IDictionary<TKey, TValue> map, TKey key, TValue value) where TValue : class => map[key] = value;

        public static IList<T> GetRange<T>(this IList<T> l, int index) => l.GetRange(index, l.Count - index);
        
        public static IList<T> GetRange<T>(this IList<T> l, int index, int count) =>
            l is List<T> x ? x.GetRange(index, count) : l.Skip(index).Take(count).ToList();

        public static void AddRange<T>(this IList<T> l, IEnumerable<T> items) {
            if (l is List<T> x)
                x.AddRange(items);
            else {
                foreach (var item in items) {
                    l.Add(item);
                }
            }
        }

        public static void ForEach<T>(this IEnumerable<T> l, Action<T> action) {
            foreach (var item in l)
                action(item);
        }
        
        public static void ForEach<T>(this IEnumerable<T> l, Action<T, int> action) {
            int i = 0;
            foreach (var item in l)
                action(item, i++);
        }
        
        public static IDictionary<TKey, TValue> emptyDictionary<TKey, TValue>() => ImmutableDictionary<TKey, TValue>.Empty;
        public static ISet<TKey> emptySet<TKey>() => ImmutableHashSet<TKey>.Empty;
        public static T[] emptyList<T>() => Array.Empty<T>();
    }
}