namespace HintServiceMeow.Core.Utilities.Parser
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;
    using HintServiceMeow.Core.Enum;
    using HintServiceMeow.Core.Interface;
    using HintServiceMeow.Core.Models.Hints;
    using HintServiceMeow.Core.Models.Parser;

    /// <summary>
    /// Immutable, player-specific safe area in HSM coordinates. Never reads Unity objects.
    /// </summary>
    internal sealed class ScreenLayout
    {
        // HSM's legacy left/right alignment anchors belong to the TMP text container,
        // not to the visible screen edges.
        internal const float TextContainerHalfWidth = 1200f;

        private static readonly Regex SizeTags = new(
            @"<noparse>.*?(?:</noparse>|$)|<size=([0-9]+(?:\.[0-9]+)?)(px|%|em)?>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        public ScreenLayout(float aspectRatio, float height, float top, float padding)
        {
            Left = -(height * aspectRatio / 2f) + padding;
            Right = (height * aspectRatio / 2f) - padding;
            Top = top + padding;
            Bottom = top + height - padding;
        }

        public float Left { get; }

        public float Right { get; }

        public float Top { get; }

        public float Bottom { get; }

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public bool Contains(TextArea area) =>
            area.Left >= Left && area.Right <= Right && area.Top >= Top && area.Bottom <= Bottom;

        public Hint Fit(Hint source, ICoordinateTools coordinates)
        {
            Hint result = new(source);
            FitSize(result, coordinates, Right - Left, Bottom - Top);
            float width = coordinates.GetTextWidth(result);
            float height = coordinates.GetTextHeight(result);
            float center = coordinates.GetXCoordinateWithAlignment(result);
            float bottom = coordinates.GetYCoordinate(result, HintVerticalAlign.Bottom);
            result.XCoordinate += Clamp(center, Left + (width / 2f), Right - (width / 2f)) - center;
            result.YCoordinate += Clamp(bottom, Top + height, Bottom) - bottom;
            return result;
        }

        public DynamicHint Prepare(DynamicHint source, ICoordinateTools coordinates)
        {
            DynamicHint result = new(source);
            result.LeftMargin = Math.Max(0f, result.LeftMargin);
            result.RightMargin = Math.Max(0f, result.RightMargin);
            result.TopMargin = Math.Max(0f, result.TopMargin);
            result.BottomMargin = Math.Max(0f, result.BottomMargin);
            FitSize(
                result,
                coordinates,
                Math.Max(1f, Right - Left - Math.Max(0, result.LeftMargin) - Math.Max(0, result.RightMargin)),
                Math.Max(1f, Bottom - Top - Math.Max(0, result.TopMargin) - Math.Max(0, result.BottomMargin)));
            float halfWidth = coordinates.GetTextWidth(result) / 2f;
            float height = coordinates.GetTextHeight(result);
            result.LeftBoundary = Math.Max(result.LeftBoundary, Left + halfWidth + Math.Max(0, result.LeftMargin));
            result.RightBoundary = Math.Min(result.RightBoundary, Right - halfWidth - Math.Max(0, result.RightMargin));
            result.TopBoundary = Math.Max(result.TopBoundary, Top + height + Math.Max(0, result.TopMargin));
            result.BottomBoundary = Math.Min(result.BottomBoundary, Bottom - Math.Max(0, result.BottomMargin));
            if (result.LeftBoundary <= result.RightBoundary && result.TopBoundary <= result.BottomBoundary)
            {
                result.TargetX = Clamp(result.TargetX, result.LeftBoundary, result.RightBoundary);
                result.TargetY = Clamp(result.TargetY, result.TopBoundary, result.BottomBoundary);
            }

            return result;
        }

        public float FitLineX(float x, float width, HintAlignment alignment)
        {
            float offset = alignment switch
            {
                HintAlignment.Left => -TextContainerHalfWidth + (width / 2f),
                HintAlignment.Right => TextContainerHalfWidth - (width / 2f),
                _ => 0f,
            };
            return Clamp(x + offset, Left + (width / 2f), Right - (width / 2f)) - offset;
        }

        private static float Clamp(float value, float minimum, float maximum) =>
            Math.Max(minimum, Math.Min(maximum, IsFinite(value) ? value : 0f));

        private static void FitSize(AbstractHint hint, ICoordinateTools coordinates, float availableWidth, float availableHeight)
        {
            // Freeze AutoContent once for both measurement and output.
            hint.Text = HintParser.RemoveIllegalTags(HintTextFormatter.GetText(hint));
            float width = coordinates.GetTextWidth(hint);
            float height = coordinates.GetTextHeight(hint);
            if (!IsFinite(width) || !IsFinite(height))
            {
                hint.Text = string.Empty;
                return;
            }

            float scale = Math.Min(1f, Math.Min(
                width > 0 ? availableWidth / width : 1f,
                height > 0 ? availableHeight / height : 1f));
            if (scale >= 1f)
                return;

            // A slight inset absorbs decimal formatting and font metric rounding.
            scale *= 0.995f;
            string scaledText = SizeTags.Replace(hint.Text, match =>
            {
                if (!match.Groups[1].Success)
                    return match.Value;

                float size = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                string unit = match.Groups[2].Value.ToLowerInvariant();

                // Match RichTextParser's established 40-unit default for relative sizes.
                size *= unit == "%" ? 0.4f : unit == "em" ? 40f : 1f;
                return "<size=" + (size * scale).ToString("0.######", CultureInfo.InvariantCulture) + ">";
            });
            hint.Text = "<size=" + (hint.FontSize * scale).ToString("0.######", CultureInfo.InvariantCulture) + ">" + scaledText + "</size>";
            hint.LineHeight *= scale;
        }
    }
}
