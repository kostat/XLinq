using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Streamx.Linq.ExTree {
    class ExpressionStack : List<Expression> {
        private bool _reduced;
        private readonly List<Expression> _ordered;
        private BranchExpression _parent;
        public MethodVisitor.LocalVariable[] LocalVariables;

        public BranchExpression Parent {
            get => _parent;
            private set {
                _parent = value;
                LocalVariables = (MethodVisitor.LocalVariable[]) value?.Parent?.LocalVariables?.Clone();
            }
        }

        public int Depth => Parent?.Depth ?? 0;

        public bool IsReduced => _reduced;

        public void Reduce() => _reduced = true;

        public ExpressionStack() : this(null) { }

        ExpressionStack(BranchExpression parent) {
            Parent = parent;
            _ordered = parent?.Parent._ordered ?? new List<Expression>(64);
        }

        public void TrackOrder(Expression e) => _ordered.Add(e);
        public void TrackOrder(Expression e, Expression after) => _ordered.Insert(_ordered.LastIndexOf(after) + 1, e);

        public void Push(Expression item) {
            Add(item);
            _ordered.Add(item);
        }

        public Expression Pop() {
            if (Count == 0)
                return null;
                
            Expression obj = Peek();
            RemoveAt(Count - 1);

            return obj;
        }

        public Expression Peek() {
            Expression obj = this[Count - 1];

            return obj;
        }
        
        public override string ToString() {
            return $"Count = {Count}";
        }

        public void Sort(IList<Expression> expressions) {
            Expression[] copy = expressions.ToArray();
            int[] indices = new int[copy.Length];
            int[] orders = new int[copy.Length];

            for (int i = 0; i < copy.Length; i++) {
                orders[i] = i;
                indices[i] = _ordered.LastIndexOf(copy[i]);
            }

            Array.Sort(orders, (i1,
                    i2) => indices[i1] - indices[i2]);

            for (int i = 0; i < copy.Length; i++)
                expressions[i] = copy[orders[i]];
        }

        public class BranchExpression : Expression {
            private readonly ExpressionStack _true;
            private readonly ExpressionStack _false;

            public Expression Test { get; }

            public ExpressionStack True => _true;

            public ExpressionStack False => _false;

            public ExpressionStack Parent { get; }

            public int Depth => Parent.Depth + 1;


            public BranchExpression(ExpressionStack parent, Expression test,
                ExpressionStack trueE = null, ExpressionStack falseE = null) {
                Parent = parent;
                Test = test;

                if (trueE != null) {
                    _true = trueE;
                    _true.Parent = (this);
                }
                else
                    _true = new ExpressionStack(this);

                if (falseE != null) {
                    _false = falseE;
                    _false.Parent = (this);
                }
                else
                    _false = new ExpressionStack(this);
            }

            public ExpressionStack Get(bool side) =>
                side ? True : False;

            public override ExpressionType NodeType => ExpressionType.Conditional;
            public override Type Type => typeof(void);

            public override string ToString() {
                return $"({Test} ? {True} : {False})";
            }
        }
    }
}