using System;
using System.Collections.Generic;
using Streamx.Linq.SQL.Grammar;
using Streamx.Linq.SQL.Grammar.Configuration;

namespace Streamx.Linq.SQL.EFCore {
    internal sealed class Configuration : IConfiguration {
        public ISet<Capability> Capabilities {
            get => ELinq.Capabilities;
            set => ELinq.Capabilities = value;
        }

        public void RegisterMethodSubstitution<T1, T2, TResult1, TResult2>(Func<T1, TResult1> @from,
            Func<T2, TResult2> to,
            bool considerParameterTypes = false) => ELinq.RegisterMethodSubstitution(@from, to, considerParameterTypes);

        public void RegisterMethodSubstitution<T1, T2, T3, T4, TResult1, TResult2>(Func<T1, T2, TResult1> @from,
            Func<T3, T4, TResult2> to,
            bool considerParameterTypes = false) =>
            ELinq.RegisterMethodSubstitution(@from, to, considerParameterTypes);

        public void RegisterMethodSubstitution<T1, T2, T3, T4, T5, T6, TResult1, TResult2>(Func<T1, T2, T5, TResult1> @from,
            Func<T3, T4, T6, TResult2> to,
            bool considerParameterTypes = false) =>
            ELinq.RegisterMethodSubstitution(@from, to, considerParameterTypes);

        public void RegisterMethodSubstitution<T1, T2, T3, T4, T5, T6, T7, T8, TResult1, TResult2>(Func<T1, T2, T5, T7, TResult1> @from,
            Func<T3, T4, T6, T8, TResult2> to,
            bool considerParameterTypes = false) =>
            ELinq.RegisterMethodSubstitution(@from, to, considerParameterTypes);

        public void RegisterMethodSubstitution<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult1, TResult2>(Func<T1, T2, T5, T7, T9, TResult1> @from,
            Func<T3, T4, T6, T8, T10, TResult2> to,
            bool considerParameterTypes = false) =>
            ELinq.RegisterMethodSubstitution(@from, to, considerParameterTypes);

        public void RegisterIdentifierQuoter(Func<string, string> quoter) => ELinq.RegisterIdentifierQuoter(quoter);
    }
}
