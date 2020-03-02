using System;
using System.Collections.Generic;
using Streamx.Linq.SQL.Grammar;
using Streamx.Linq.SQL.Grammar.Configuration;

namespace Streamx.Linq.SQL.MySQL {
    public static class SQL {
        
        // Clauses

        [Function(OmitParentheses = true)]
        public static IClause ON_DUPLICATE_KEY_UPDATE(Action updates) {
            throw new NotSupportedException();
        }
        
        [Function]
        public static int LENGTH(String expression) {
            throw new NotSupportedException();
        }

        /**
     * Use {@link co.streamx.fluent.SQL.SQL#ALL(Comparable...) ALL} or
     * {@link co.streamx.fluent.SQL.SQL#DISTINCT(Comparable...) DISTINCT} to pass multiple expressions
     */
        [Function(UnderscoresAsBlanks = false)]
        public static String GROUP_CONCAT(IComparable expr) {
            throw new NotSupportedException();
        }

        /**
     * Use {@link co.streamx.fluent.SQL.SQL#ALL(Comparable...) ALL} or
     * {@link co.streamx.fluent.SQL.SQL#DISTINCT(Comparable...) DISTINCT} to pass multiple expressions
     */
        [Function(UnderscoresAsBlanks = false, OmitArgumentsDelimiter = true)]
        public static String GROUP_CONCAT(IComparable expr, IWindowFrame order) {
            throw new NotSupportedException();
        }

        [Function(UnderscoresAsBlanks = false, OmitArgumentsDelimiter = true)]
        public static String GROUP_CONCAT(IComparable expr,
            [Context(ParameterContext.Inherit, Format = "SEPARATOR {0}")]
            String separator) {
            throw new NotSupportedException();
        }

        [Function(UnderscoresAsBlanks = false, OmitArgumentsDelimiter = true)]
        public static String GROUP_CONCAT(IComparable expr,
            IWindowFrame order,
            [Context(ParameterContext.Inherit, Format = "SEPARATOR {0}")]
            String separator) {
            throw new NotSupportedException();
        }
        
        
        [Function(OmitParentheses = true)]
        public static IInsertClause INSERT(params Modifier[] hints) {
            throw new NotSupportedException();
        }

        public static void RegisterVendorCapabilities(this IConfiguration config) {
            config.Capabilities = new HashSet<Capability>(new[] {Capability.TABLE_AS_ALIAS});

            config.RegisterMethodSubstitution((String s) => s.Length, (String s) => SQL.LENGTH(s));
            config.RegisterGenericCapabilities();
        }
    }
}
