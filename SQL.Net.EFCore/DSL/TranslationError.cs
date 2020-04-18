using System;

namespace Streamx.Linq.SQL.EFCore.DSL {
    sealed class TranslationError {
        public static readonly TranslationError INVALID_FROM_PARAM =
            new TranslationError("Only @Entity, @Tuple or scalar = new TranslationError(primitive) types are allowed in FROM context: {0}");

        public static readonly TranslationError CANNOT_CALCULATE_TABLE_REFERENCE = new TranslationError("Cannot calculate table reference from: {0}");

        public static readonly TranslationError CANNOT_DEREFERENCE_PARAMETERS =
            new TranslationError("Cannot access parameters properties inside SQL expression: [{0}].[{1}]");

        public static readonly TranslationError REQUIRES_EXTERNAL_PARAMETER = new TranslationError(
            "Parameter method accepts external parameters only; as an object. "
            + "Calculations and expressions must be performed out of Lambda. Received: {0}");

        public static readonly TranslationError UNSUPPORTED_EXPRESSION_TYPE = new TranslationError("Unsupported operator: {0}");
        public static readonly TranslationError INSTANCE_NOT_JOINTABLE = new TranslationError("Not a JoinTable instance: {0}");
        public static readonly TranslationError NOT_PROPERTY_CALL = new TranslationError("Must pass a getter call: {0}");

        public static readonly TranslationError ASSOCIATION_NOT_INITED =
            new TranslationError("Association not initialized for {0}. Missed join() call?");

        public static readonly TranslationError ALIAS_NOT_SPECIFIED = new TranslationError("Alias not specified for index: {0}");
        public static readonly TranslationError NO_COLUMN_DEFINITION_PROVIDED = new TranslationError("Column definitions are not provided. Use @using() and Row() methods to capture column names.");
        public static readonly TranslationError SECONDARY_TABLE_NOT_FOUND = new TranslationError("Cannot find secondary table {1} declared on {0} entity");

        public static readonly TranslationError SECONDARY_TABLE_NOT_CONSTANT =
            new TranslationError("Secondary table name must be a constant.  = new TranslationError({0})");

        public static readonly TranslationError INHERITANCE_STRATEGY_NOT_FOUND = new TranslationError("Inheritance strategy not found on {0} entity");

        public static readonly TranslationError REQUIRES_LICENSE = new TranslationError("{0} requires a license. Get one at https://fluentjpa.com");

        public static readonly TranslationError UNMAPPED_FIELD =
            new TranslationError("Cannot translate property: {0}.{1}. Ensure the type {0} is mapped. Note, that ELINQ custom methods must be either static, interface default or attributed with [Local]");
        
        public static readonly TranslationError UNEXPECTED_ASSOCIATION =
            new TranslationError("Unexpected entity association: {0} = {1}");

        public TranslationError(string pattern) {
            Pattern = pattern;
        }

        private string Pattern { get; }

        public SystemException getError(params Object[] args) => new InvalidOperationException(String.Format(Pattern, args));

        public SystemException getError(Exception cause, params Object[] args) => new InvalidOperationException(String.Format(Pattern, args), cause);
    }
}