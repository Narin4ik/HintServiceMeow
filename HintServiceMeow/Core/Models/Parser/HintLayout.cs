namespace HintServiceMeow.Core.Models.Parser
{
    using System.Collections.Generic;

    /// <summary>
    /// A display-local layout. Moving or scaling it never changes the source hint.
    /// </summary>
    internal sealed class HintLayout
    {
        public HintLayout(IReadOnlyList<LineInfo> lines, float fontSize, float lineHeight, float scale, float width, float height)
        {
            Lines = lines;
            FontSize = fontSize;
            LineHeight = lineHeight;
            Scale = scale;
            Width = width;
            Height = height;
        }

        public IReadOnlyList<LineInfo> Lines { get; }

        public float FontSize { get; }

        public float LineHeight { get; }

        public float Scale { get; }

        public float Width { get; }

        public float Height { get; }

        public float CenterX { get; private set; }

        public float Bottom { get; private set; }

        public float Top => Bottom - Height;

        public TextArea Area => new()
        {
            Left = CenterX - (Width / 2f),
            Right = CenterX + (Width / 2f),
            Top = Top,
            Bottom = Bottom,
        };

        public void MoveTo(float centerX, float bottom)
        {
            CenterX = centerX;
            Bottom = bottom;
        }
    }
}
