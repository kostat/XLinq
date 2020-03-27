using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using Streamx.Linq.ExTree;
using Streamx.Linq.SQL.EFCore.DSL;
using Streamx.Linq.SQL.Grammar;
using Streamx.Linq.SQL.Grammar.Configuration;

namespace Streamx.Linq.SQL.EFCore {
    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// XLinq globals
    /// </summary>
    public static class XLinq {

        static XLinq() {
            var ver = Environment.Version;
            if (ver.Major > 4)
                throw new PlatformNotSupportedException();
        }
        
        private static readonly PropertyInfo DEBUG_VIEW =
#if DEBUG
            typeof(Expression).GetProperty("DebugView",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty);
#else
            null;
#endif
        /// <summary>
        /// Access XLinq global configuration 
        /// </summary>
        public static IConfiguration Configuration { get; } = new Configuration();

        internal static ISet<Capability> Capabilities { get; set; } = Collections.emptySet<Capability>();

        internal struct Substitution {
            public Substitution(IList<ParameterExpression> parameters, Expression expression) {
                Parameters = parameters;
                Expression = expression;
            }

            public IList<ParameterExpression> Parameters { get; }
            public Expression Expression { get; }
        }

        private struct SubstitutionKey : IEquatable<SubstitutionKey> {
            public SubstitutionKey(MethodBase method, bool considerParameterTypes) {
                Name = method.Name;
                DeclaringType = method.DeclaringType;
                ParameterCount = method.GetParameters().Length;
                Attributes = method.Attributes & ~(MethodAttributes.Abstract | MethodAttributes.Final);
                ConsiderParameterTypes = considerParameterTypes;
                Parameters = considerParameterTypes ? method.GetParameters() : null;
            }

            private string Name { get; }
            private Type DeclaringType { get; }
            private int ParameterCount { get; }
            private MethodAttributes Attributes { get; }
            private bool ConsiderParameterTypes { get; }
            private ParameterInfo[] Parameters { get; }

            public bool Equals(SubstitutionKey other) {
                var simple = Name == other.Name &&
                             Attributes == other.Attributes &&
                             ParameterCount == other.ParameterCount &&
                             (!ConsiderParameterTypes || !other.ConsiderParameterTypes || Equals(Parameters, other.Parameters));

                if (!simple)
                    return false;

                if (DeclaringType == other.DeclaringType || (Attributes & MethodAttributes.Static) != 0)
                    return true;

                if (DeclaringType == null ^ other.DeclaringType == null)
                    return false;

                // ReSharper disable once PossibleNullReferenceException
                var declaring = DeclaringType.IsGenericType ? DeclaringType.GetGenericTypeDefinition() : DeclaringType;
                // ReSharper disable once PossibleNullReferenceException
                var otherDeclaring = other.DeclaringType.IsGenericType ? other.DeclaringType.GetGenericTypeDefinition() : other.DeclaringType;

                if (declaring == otherDeclaring || declaring.IsSubclassOf(otherDeclaring) || otherDeclaring.IsSubclassOf(declaring))
                    return true;

                if (declaring.IsClass == otherDeclaring.IsClass)
                    return false;

                return otherDeclaring.IsClass ? ContainsInterface(otherDeclaring, declaring) : ContainsInterface(declaring, otherDeclaring);
            }

            private static bool ContainsInterface(Type @class, Type iface) {
                var ifaces = @class.GetInterfaces();

                foreach (var i in ifaces) {
                    if (i.MetadataToken == iface.MetadataToken && i.Module == iface.Module)
                        return true;
                }

                return false;
            }

            public override bool Equals(object obj) {
                return obj is SubstitutionKey other && Equals(other);
            }

            public override int GetHashCode() {
                return HashCode.Combine(Name, ParameterCount, (int) Attributes);
            }

            public override string ToString() {
                return $"{DeclaringType}.{Name}";
            }
        }

        private static readonly IDictionary<SubstitutionKey, Substitution> SUBSTITUTIONS = new ConcurrentDictionary<SubstitutionKey, Substitution>();
        internal static Func<String, String> Quoter { get; private set; } = s => s;

        internal static void RegisterMethodSubstitution<T1, T2, TResult1, TResult2>(Func<T1, TResult1> from,
            Func<T2, TResult2> to,
            bool considerParameterTypes) {

            RegisterMethodSubstitiution0(@from, to, considerParameterTypes);
        }

        internal static void RegisterMethodSubstitution<T1, T2, T3, T4, TResult1, TResult2>(Func<T1, T2, TResult1> from,
            Func<T3, T4, TResult2> to,
            bool considerParameterTypes) {

            RegisterMethodSubstitiution0(@from, to, considerParameterTypes);
        }

        internal static void RegisterMethodSubstitution<T1, T2, T3, T4, T5, T6, TResult1, TResult2>(Func<T1, T2, T5, TResult1> from,
            Func<T3, T4, T6, TResult2> to,
            bool considerParameterTypes) {

            RegisterMethodSubstitiution0(@from, to, considerParameterTypes);
        }

        internal static void RegisterMethodSubstitution<T1, T2, T3, T4, T5, T6, T7, T8, TResult1, TResult2>(Func<T1, T2, T5, T7, TResult1> from,
            Func<T3, T4, T6, T8, TResult2> to,
            bool considerParameterTypes) {

            RegisterMethodSubstitiution0(@from, to, considerParameterTypes);
        }

        internal static void RegisterMethodSubstitution<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult1, TResult2>(Func<T1, T2, T5, T7, T9, TResult1> from,
            Func<T3, T4, T6, T8, T10, TResult2> to,
            bool considerParameterTypes) {

            RegisterMethodSubstitiution0(@from, to, considerParameterTypes);
        }

        private static void RegisterMethodSubstitiution0<T1, T2>(T1 @from, T2 to, bool considerParameterTypes)
            where T1 : MulticastDelegate
            where T2 : MulticastDelegate {
            var parsedFrom = ExpressionTree.Parse(@from);
            var parsedTo = ExpressionTree.Parse(to);

            // PrintExpression(parsedFrom);
            // PrintExpression(parsedTo);

            var body = (BlockExpression) parsedFrom.Body;
            if (!(body.Result is MethodCallExpression methodCall))
                throw new ArgumentException("From lambda must contain a single method.");

            body = (BlockExpression) parsedTo.Body;
            SUBSTITUTIONS[new SubstitutionKey(methodCall.Method, considerParameterTypes)] = new Substitution(parsedTo.Parameters, body.Result);
        }

        internal static bool GetSubstitition(MethodBase method, out Substitution substitution) =>
            SUBSTITUTIONS.TryGetValue(new SubstitutionKey(method, true), out substitution);

        [Conditional("DEBUG")]
        internal static void PrintExpression(Expression parsed) {
            Console.WriteLine($"Parsed: {DEBUG_VIEW.GetValue(parsed)}");
        }

        internal static void RegisterIdentifierQuoter(Func<string, string> quoter) => 
            Quoter = quoter ?? (s => s);
    }
}
