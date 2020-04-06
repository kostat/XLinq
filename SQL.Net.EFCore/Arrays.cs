using System;

namespace Streamx.Linq.SQL.EFCore {
    static class Arrays {
        public static void Fill<T>(this T[] array, T value) {
#if NETFRAMEWORK
            for (var i = 0; i < array.Length; i++) {
                array[i] = value;
            }
#else
            Array.Fill(array, value);
#endif
        }
    }
}
