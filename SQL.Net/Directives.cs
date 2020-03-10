using System;
using System.Runtime.CompilerServices;
using Streamx.Linq.SQL.Grammar;

namespace Streamx.Linq.SQL {
    public static class Directives {
        [SubQuery]
        public static T SubQuery<T>(Func<IQueryResult<T>> query) {
            throw new NotSupportedException();
        }

        [SubQuery]
        public static T SubQuery<TEntity0, T>(Func<TEntity0, IQueryResult<T>> query) {
            throw new NotSupportedException();
        }

        [SubQuery]
        public static T SubQuery<TEntity0, TEntity1, T>(Func<TEntity0, TEntity1, IQueryResult<T>> query) {
            throw new NotSupportedException();
        }

        [SubQuery]
        public static T SubQuery<TEntity0, TEntity1, TEntity2, T>(Func<TEntity0, TEntity1, TEntity2, IQueryResult<T>> query) {
            throw new NotSupportedException();
        }

        [SubQuery]
        public static T SubQuery<TEntity0, TEntity1, TEntity2, TEntity3, T>(Func<TEntity0, TEntity1, TEntity2, TEntity3, IQueryResult<T>> query) {
            throw new NotSupportedException();
        }

        [SubQuery]
        public static T SubQuery<TEntity0, TEntity1, TEntity2, TEntity3, TEntity4, T>(
            Func<TEntity0, TEntity1, TEntity2, TEntity3, TEntity4, IQueryResult<T>> query) {
            throw new NotSupportedException();
        }

        [SubQuery]
        public static T SubQuery<TEntity0, TEntity1, TEntity2, TEntity3, TEntity4, TEntity5, T>(
            Func<TEntity0, TEntity1, TEntity2, TEntity3, TEntity4, TEntity5, IQueryResult<T>> query) {
            throw new NotSupportedException();
        }

        /**
     * lets specify a window frame in OVER clause
     */
        [Function("", OmitParentheses = true)]
        public static IWindowFrame WindowFrame() {
            throw new NotSupportedException();
        }

        /**
     * Starts Window Function (OVER clause)
     */
        [Function("", OmitParentheses = true)]
        public static IAggregateGroup<T> aggregateBy<T>(T aggregateFunction) where T : IComparable {
            throw new NotSupportedException();
        }
        
        [Function("", OmitParentheses = true)]
        public static IAggregateGroup<T> aggregateBy<T>(T? aggregateFunction) where T : struct, IComparable {
            throw new NotSupportedException();
        }

        [Alias]
        // ReSharper disable once InconsistentNaming
        public static IAlias<T> @as<T>(this T field,
            [Context(ParameterContext.Alias)] T alias) where T : IComparable {
            throw new NotSupportedException();
        }
        
        [Alias]
        // ReSharper disable once InconsistentNaming
        public static IAlias<T> @as<T>(this T field) where T : IComparable {
            throw new NotSupportedException();
        }

        [Alias]
        // ReSharper disable once InconsistentNaming
        public static IAlias<T> @as<T>(this T field,
            [Context(ParameterContext.Alias)] T? alias) where T : struct, IComparable {
            throw new NotSupportedException();
        }
        
        [Alias]
        // ReSharper disable once InconsistentNaming
        public static IAlias<T> @as<T>(this T? field,
            [Context(ParameterContext.Alias)] T alias) where T : struct, IComparable {
            throw new NotSupportedException();
        }

        [Alias]
        // ReSharper disable once InconsistentNaming
        public static IAlias<T> @as<T>(this T? field,
            [Context(ParameterContext.Alias)] T? alias) where T : struct, IComparable {
            throw new NotSupportedException();
        }
        
        [Alias]
        // ReSharper disable once InconsistentNaming
        public static IAlias<T> @as<T>(this T? field) where T : struct, IComparable {
            throw new NotSupportedException();
        }

        [CommonTableExpression(CommonTableExpressionType.Decorator)]
        [Function("", OmitParentheses = true, ParameterContext = ParameterContext.FromWithoutAlias, ParameterContextCapabilities = new[] {
            nameof(Capability.ALIAS_INSERT)
        })]
        [Operator]
        [ViewDeclaration]
        // ReSharper disable once InconsistentNaming
        public static IProjection<T, TTuple> @using<T, TTuple>(this T tableReference,
            [Context(ParameterContext.Alias)] TTuple tuple)
            where T : class
            where TTuple : struct, ITuple {
            throw new NotSupportedException();
        }
        
        /**
     * Block terminator in SQL
     */
        [BlockTerminator]
        [Function(";", OmitParentheses = true)]
        public static void Semicolon() {
            throw new NotSupportedException();
        }

        [TableDeclaration]
        public static T ToTable<T>(String table, String schema = null) {
            throw new NotSupportedException();
        }

        /*[CommonTableExpression(CommonTableExpressionType.Decorator)]
        [Function("", OmitParentheses = true, ParameterContext = ParameterContext.FromWithoutAlias, ParameterContextCapabilities = new[] {
            nameof(Capability.ALIAS_INSERT)
        })]
        [Operator]
        [ViewDeclaration]
        // ReSharper disable once InconsistentNaming
        public static IView<ValueTuple<T1, T2>> @using<T, T1, T2>(this T tableReference,
            [Context(ParameterContext.Alias)] T1 column1,
            [Context(ParameterContext.Alias)] T2 column2)
            where T : class
            where T1 : IComparable<T1>, IComparable
            where T2 : IComparable<T1>, IComparable {
            throw new NotSupportedException();
        }

        [CommonTableExpression(CommonTableExpressionType.Decorator)]
        [Function("", OmitParentheses = true, ParameterContext = ParameterContext.FromWithoutAlias, ParameterContextCapabilities = new[] {
            nameof(Capability.ALIAS_INSERT)
        })]
        [Operator]
        [ViewDeclaration]
        // ReSharper disable once InconsistentNaming
        public static IView<ValueTuple<T1, T2>> @using<T, T1, T2, T3>(this T tableReference,
            [Context(ParameterContext.Alias)] T1 column1,
            [Context(ParameterContext.Alias)] T2 column2,
            [Context(ParameterContext.Alias)] T3 column3)
            where T : class
            where T1 : IComparable<T1>, IComparable
            where T2 : IComparable<T1>, IComparable
            where T3 : IComparable<T1>, IComparable {
            throw new NotSupportedException();
        }*/
    }
}
