namespace Codec.Text
{
    using System;

    public struct Indent(string indentation = "    ")
    {
        public string Indentation { get; set; } = indentation;

        public int Depth { get; set; }

        public static Indent operator ++(Indent indent) =>
            indent with { Depth = checked(indent.Depth + 1) };

        public static Indent operator --(Indent indent) =>
            indent with { Depth = checked(indent.Depth - 1) };

        public static Indent operator +(Indent indent, int amount) =>
            indent with { Depth = checked(indent.Depth + amount) };

        public static Indent operator -(Indent indent, int amount) =>
            indent with { Depth = checked(indent.Depth - amount) };

        public override string ToString()
        {
            if (this.Depth == 0 || this.Indentation.Length == 0)
            {
                return string.Empty;
            }

            return this.Depth < 0 ? new string('<', -this.Depth) : string.Create(this.Indentation.Length * this.Depth, this, static (span, state) =>
            {
                ReadOnlySpan<char> chunk = state.Indentation;
                for (var i = 0; i < state.Depth; i++)
                {
                    chunk.CopyTo(span.Slice(i * chunk.Length, chunk.Length));
                }
            });
        }
    }
}
