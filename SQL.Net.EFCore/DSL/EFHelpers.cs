using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Streamx.Linq.SQL.Grammar;

namespace Streamx.Linq.SQL.EFCore.DSL {
    partial class DSLInterpreter {
        
        private static readonly int HAS_VALUE = typeof(Nullable<>).GetProperty("HasValue").GetMethod.MetadataToken;
        
        public bool isCollection(Type entity) {
            return false;
            //throw new System.NotImplementedException();
        }

        public bool isScalar(Type entity) {
            return false;
            //throw new System.NotImplementedException();
        }

        public bool isEntity(Type entity) {
            throw new System.NotImplementedException();
        }

        public bool isEntityLike(Type entity) {
            // throw new System.NotImplementedException();
            if (entity.IsClass)
                return Type.GetTypeCode(entity) == TypeCode.Object;

            if (!entity.IsInterface)
                return false;

            return !typeof(IComparable).IsAssignableFrom(entity);
        }

        public bool isEmbeddable(Type entity) {
            return false;
            throw new System.NotImplementedException();
        }

        public bool isEmbedded(MemberInfo field) {
            return false;
            throw new System.NotImplementedException();
        }

        public Association getAssociation(Expression left,
            Expression right) {

            if (left is MethodCallExpression method && method.Method.IsSpecialName)
                return FindAssociation(method, true);

            if (right is MethodCallExpression method1 && method1.Method.IsSpecialName)
                return FindAssociation(method1, false);

            throw TranslationError.UNEXPECTED_ASSOCIATION.getError(left, right);
        }

        private Association FindAssociation(MethodCallExpression method, bool leftOwner) {
            return FindAssociation(method.Object.Type, method.Method, leftOwner);
        }

        private Association FindAssociation(Type target, MethodInfo methodBase, bool leftOwner) {
            var navigation = FindProperty(target, methodBase, (e, mi) => e.FindNavigation(mi));
            var foreignKey = navigation.ForeignKey;
            var foreignKeyProperties = foreignKey.Properties.Select(p => p.GetColumnName().AsSequence()).ToList();
            var principalKeyProperties = foreignKey.PrincipalKey.Properties.Select(p => p.GetColumnName().AsSequence()).ToList();

            return new Association(foreignKeyProperties, principalKeyProperties, leftOwner);
        }

        public ISequence<char> calcOverrides(ISequence<char> instance,
            MemberInfo field,
            IDictionary<String, ISequence<char>> secondaryResolver) {
            return instance;
            // throw new System.NotImplementedException();
        }

        public IdentifierPath getColumnNameFromProperty(MethodInfo methodBase, Type target) {
            if (methodBase.IsSpecialName) {

                if (isEntityLike(methodBase.ReturnType)) {
                    var assoc = FindAssociation(target, methodBase, true);
                    if (assoc == null)
                        throw TranslationError.UNMAPPED_FIELD.getError(methodBase);
                    return new IdentifierPath.MultiColumnIdentifierPath(methodBase.Name, 
                        _ => assoc, null);
                }
                
                var prop = FindProperty(target, methodBase, (e, mi) => e.FindProperty(mi));
                if (prop == null && !target.IsDefined(typeof(TupleAttribute)))
                    throw TranslationError.UNMAPPED_FIELD.getError(methodBase);
                var columnName = prop == null ? RemoveSpecialPrefix(methodBase.Name) : prop.GetColumnName();
                return new IdentifierPath.Resolved(columnName.AsSequence(), methodBase.DeclaringType, methodBase.Name, null);
            }

            return new IdentifierPath.Resolved(methodBase.Name.AsSequence(), methodBase.DeclaringType, methodBase.Name, null);
        }

        private static string RemoveSpecialPrefix(string specialName) {
            var uIndex = specialName.IndexOf('_');
            return uIndex < 0 ? specialName : specialName.Substring(uIndex + 1);
        }

        private T FindProperty<T>(Type target, MethodInfo methodBase, Func<IEntityType, MemberInfo, T> property)
            where T : class {
            var propInfo = GetPropertyInfo(methodBase);

            var entityType = model.FindEntityType(target ?? methodBase.DeclaringType);
            if (entityType == null)
                return target?.BaseType != null ? FindProperty<T>(target.BaseType, methodBase, property) : null;
            return property(entityType, propInfo);
        }

        public static PropertyInfo GetPropertyInfo(MethodBase methodBase) {
            var bindingFlags = methodBase.IsStatic ? BindingFlags.Static : BindingFlags.Instance;
            bindingFlags |= methodBase.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic;
            var propInfo = methodBase.DeclaringType.GetProperty(methodBase.Name.Substring(4), bindingFlags);
            return propInfo;
        }

        public String getTableName(Type entity) {
            var entityType = model.FindRuntimeEntityType(entity);
            // TODO: throw if null
            var schema = entityType.GetSchema();
            var tableName = entityType.GetTableName();
            return schema != null ? schema + DOT + tableName : tableName;
        }
    }
}