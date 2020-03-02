namespace Streamx.Linq.ExTree {
    internal struct Label {
        public static Label Finish => new Label(-1);

        public Label(int offset) {
            this.Offset = offset;
        }

        private int Offset { get; }

        public override string ToString() {
            return $"IL_{Offset:x4}";
        }
    }
}