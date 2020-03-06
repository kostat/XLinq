using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Streamx.Linq.SQL.Grammar;
using static Streamx.Linq.SQL.SQL;
using static Streamx.Linq.SQL.Directives;

namespace Streamx.Linq.SQL {
    public static class Library {
        public static TProperty pick<TEntity, TProperty>(TEntity entity, TProperty expression)
            where TEntity : class, new()
            where TProperty : IComparable {
            return SubQuery(() => {
                var r = SELECT(expression);
                FROM(entity);
                return r.AsSingle();
            });
        }
        
        public static TProperty? pick<TEntity, TProperty>(TEntity entity, TProperty? expression)
            where TEntity : class, new()
            where TProperty : struct, IComparable {
            return SubQuery(() => {
                var r = SELECT(expression);
                FROM(entity);
                return r.AsSingle();
            });
        }
        
        public static ICollection<TProperty> collect<TEntity, TProperty>(TEntity entity, TProperty expression)
            where TEntity : class, new()
            where TProperty : IComparable {
            return SubQuery(() => {
                var r = SELECT(expression);
                FROM(entity);
                return r.AsCollection();
            });
        }
        
        public static ICollection<TProperty?> collect<TEntity, TProperty>(TEntity entity, TProperty? expression)
            where TEntity : class, new()
            where TProperty : struct, IComparable {
            return SubQuery(() => {
                var r = SELECT(expression);
                FROM(entity);
                return r.AsCollection();
            });
        }
        
        private static readonly Keyword ASTERISK  = new Keyword("*");
        
        public static long COUNT() {
            return AggregateFunctions.COUNT(ASTERISK);
        }
    }
}