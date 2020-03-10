using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Streamx.Linq.ExTree;
using Streamx.Linq.SQL.EFCore.DSL;
using Streamx.Linq.SQL.Grammar;

[assembly: InternalsVisibleTo("SQL.Net.EFCore.Tests")]

namespace Streamx.Linq.SQL.EFCore {
    // ReSharper disable once InconsistentNaming
    public static class EFCoreExtensions {
        static EFCoreExtensions() {
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => Equals(s1, s2), (object s1, object s2) => s1 == s2);

            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => s1 + s2, (int s1, int s2) => s1 + s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => s1 - s2, (int s1, int s2) => s1 - s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => s1 * s2, (int s1, int s2) => s1 * s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => s1 / s2, (int s1, int s2) => s1 / s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => s1 % s2, (int s1, int s2) => s1 % s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => s1 & s2, (int s1, int s2) => s1 & s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => s1 | s2, (int s1, int s2) => s1 | s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => s1 ^ s2, (int s1, int s2) => s1 ^ s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, int s2) => s1 << s2, (int s1, int s2) => s1 << s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, int s2) => s1 >> s2, (int s1, int s2) => s1 >> s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => s1 == s2, (object s1, object s2) => s1 == s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => s1 != s2, (object s1, object s2) => s1 != s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => s1 < s2, (int s1, int s2) => s1 < s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => s1 > s2, (int s1, int s2) => s1 > s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => s1 <= s2, (int s1, int s2) => s1 <= s2);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1, Point s2) => s1 >= s2, (int s1, int s2) => s1 >= s2);

            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1) => +s1, (int s1) => +s1);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1) => -s1, (int s1) => -s1);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1) => !s1, (bool s1) => !s1);
            ExLINQ.Configuration.RegisterMethodSubstitution((Point s1) => ~s1, (int s1) => ~s1);
            ExLINQ.Configuration.RegisterMethodSubstitution((int s1) => (Point) s1, (int s1) => s1);
        }

        // ReSharper disable once ClassNeverInstantiated.Local
#pragma warning disable 660,661
        private sealed class Point
#pragma warning restore 660,661
        {
            public static Point operator +(Point a) => throw new NotImplementedException();
            public static Point operator -(Point a) => throw new NotImplementedException();
            public static Point operator !(Point a) => throw new NotImplementedException();
            public static Point operator ~(Point a) => throw new NotImplementedException();
            public static implicit operator Point(int a) => throw new NotImplementedException();

            public static Point operator +(Point a, Point b) => throw new NotImplementedException();
            public static Point operator -(Point a, Point b) => throw new NotImplementedException();
            public static Point operator *(Point a, Point b) => throw new NotImplementedException();
            public static Point operator /(Point a, Point b) => throw new NotImplementedException();
            public static Point operator %(Point a, Point b) => throw new NotImplementedException();
            public static Point operator &(Point a, Point b) => throw new NotImplementedException();
            public static Point operator |(Point a, Point b) => throw new NotImplementedException();
            public static Point operator ^(Point a, Point b) => throw new NotImplementedException();
            public static Point operator <<(Point a, int b) => throw new NotImplementedException();
            public static Point operator >>(Point a, int b) => throw new NotImplementedException();
            public static bool operator ==(Point a, Point b) => throw new NotImplementedException();
            public static bool operator !=(Point a, Point b) => throw new NotImplementedException();
            public static bool operator <(Point a, Point b) => throw new NotImplementedException();
            public static bool operator >(Point a, Point b) => throw new NotImplementedException();
            public static bool operator <=(Point a, Point b) => throw new NotImplementedException();
            public static bool operator >=(Point a, Point b) => throw new NotImplementedException();
        }

        public static IQueryable<TEntity> Query<TEntity>(this DbSet<TEntity> source, Func<IQueryResult<TEntity>> query)
            where TEntity : class =>
            Query0(source, query);

        public static IQueryable<TEntity> Query<TEntity, TEntity1>(this DbSet<TEntity> source, Func<TEntity1, IQueryResult<TEntity>> query)
            where TEntity : class
            where TEntity1 : class =>
            Query0(source, query);

        public static IQueryable<TEntity> Query<TEntity, TEntity1, TEntity2>(this DbSet<TEntity> source, Func<TEntity1, TEntity2, IQueryResult<TEntity>> query)
            where TEntity : class
            where TEntity1 : class
            where TEntity2 : class =>
            Query0(source, query);

        public static IQueryable<TEntity> Query<TEntity, TEntity1, TEntity2, TEntity3>(this DbSet<TEntity> source,
            Func<TEntity1, TEntity2, TEntity3, IQueryResult<TEntity>> query)
            where TEntity : class
            where TEntity1 : class
            where TEntity2 : class
            where TEntity3 : class =>
            Query0(source, query);

        public static IQueryable<TEntity> Query<TEntity, TEntity1, TEntity2, TEntity3, TEntity4>(this DbSet<TEntity> source,
            Func<TEntity1, TEntity2, TEntity3, TEntity4, IQueryResult<TEntity>> query)
            where TEntity : class
            where TEntity1 : class
            where TEntity2 : class
            where TEntity3 : class =>
            Query0(source, query);

        private static IQueryable<TEntity> Query0<TEntity, TDelegate>(DbSet<TEntity> source, TDelegate query)
            where TEntity : class
            where TDelegate : MulticastDelegate {
            var qsql = GetQuerySQL(source, query, out var @params);
            return source.FromSqlRaw(qsql, @params);
        }

        public static int Query(this DatabaseFacade source, Action query) =>
            Query0(source, query);

        public static int Query<TEntity>(this DatabaseFacade source, Action<TEntity> query)
            where TEntity : class =>
            Query0(source, query);

        public static int Query<TEntity, TEntity1>(this DatabaseFacade source, Action<TEntity, TEntity1> query)
            where TEntity : class
            where TEntity1 : class =>
            Query0(source, query);

        public static int Query<TEntity, TEntity1, TEntity2>(this DatabaseFacade source, Action<TEntity, TEntity1, TEntity2> query)
            where TEntity : class
            where TEntity1 : class
            where TEntity2 : class =>
            Query0(source, query);

        public static int Query<TEntity, TEntity1, TEntity2, TEntity3>(this DatabaseFacade source, Action<TEntity, TEntity1, TEntity2, TEntity3> query)
            where TEntity : class
            where TEntity1 : class
            where TEntity2 : class
            where TEntity3 : class =>
            Query0(source, query);

        public static int Query<TEntity, TEntity1, TEntity2, TEntity3, TEntity4>(this DatabaseFacade source,
            Action<TEntity, TEntity1, TEntity2, TEntity3, TEntity4> query)
            where TEntity : class
            where TEntity1 : class
            where TEntity2 : class
            where TEntity3 : class
            where TEntity4 : class =>
            Query0(source, query);

        public static int Query<TEntity, TEntity1, TEntity2, TEntity3, TEntity4, TEntity5>(this DatabaseFacade source,
            Action<TEntity, TEntity1, TEntity2, TEntity3, TEntity4, TEntity5> query)
            where TEntity : class
            where TEntity1 : class
            where TEntity2 : class
            where TEntity3 : class
            where TEntity4 : class
            where TEntity5 : class =>
            Query0(source, query);

        private static int Query0<TDelegate>(DatabaseFacade db, TDelegate query)
            where TDelegate : MulticastDelegate {
            var qsql = GetQuerySQL(db, query, out var @params);
            return db.ExecuteSqlRaw(qsql, @params);
        }

        internal static string GetQuerySQL<TDelegate>(IInfrastructure<IServiceProvider> source, TDelegate query, out object[] @params)
            where TDelegate : MulticastDelegate {
            var parsed = (Expression) ExpressionTree.Parse(query);

            ExLINQ.PrintExpression(parsed);

            var norm = new Normalizer();
            parsed = norm.Visit(parsed);

            var dsl = new DSLInterpreter(source.GetService<IModel>());
            var fvisited = dsl.Visit(parsed).As<Func<Func<ISequence<char>>>>();
            var visited = fvisited();
            var qsql = visited().ToString();
            @params = dsl.IndexedParameters.ToArray();
#if DEBUG
            Console.WriteLine(qsql);
#endif
            return qsql;
        }
    }
}
