using System;
using System.Text;

namespace Streamx.Linq.SQL.EFCore.DSL {
    static class Strings {
        public static ISequence<char> AsSequence(this string s) => StringSequence.From(s);
        public static ISequence<char> AsSequence(this StringBuilder s) => StringBuilderSequence.From(s);
        public static readonly ISequence<char> Empty = String.Empty.AsSequence();

        public static bool isNullOrEmpty(this ISequence<char> seq) {
            return seq == null || seq.IsEmpty;
        }

        public static bool equals(this ISequence<char> source,
            ISequence<char> prefix) {
            if (source == null) {
                return prefix == null;
            }
            else if (prefix == null)
                return false;

            return compare(source, 0, prefix, 0, source.Length) == 0;
        }

        public static bool startsWith(this ISequence<char> left,
            ISequence<char> right) {
            int length = right.Length;
            if (length > left.Length)
                return false;
            return compare(left, 0, right, 0, length) == 0;
        }

        public static int compare(this ISequence<char> lseq,
            int lstart,
            ISequence<char> rseq,
            int rstart,
            int length) {
            for (int i = 0; i < length; i++) {
                char l = lseq[lstart + i];
                char r = rseq[rstart + i];
                if (l != r)
                    return l - r;
            }

            return 0;
        }

        public static int lastIndexOf(this ISequence<char> source,
            char ch) {
            for (int i = source.Length - 1; i >= 0; i--) {
                if (source[i] == ch) {
                    return i;
                }
            }

            return -1;
        }

        public static int indexOf(this ISequence<char> source,
            char ch) {
            return indexOf(source, ch, 0);
        }

        public static int indexOf(this ISequence<char> source,
            char ch,
            int start) {
            for (int i = start; i < source.Length; i++) {
                if (source[i] == ch) {
                    return i;
                }
            }

            return -1;
        }

        public static ISequence<char> trim(this ISequence<char> source) {
            if (source == null)
                return source;
            int len = source.Length;
            if (len == 0)
                return source;
            int start = 0;
            while (start < len && (source[start] <= ' '))
                start++;
            if (start == len)
                return Empty;
            while (len > start && (source[--len] <= ' '))
                ;

            return source.SubSequence(start, ++len);
        }
    }
}