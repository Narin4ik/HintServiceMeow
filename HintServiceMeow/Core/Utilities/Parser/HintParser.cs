namespace HintServiceMeow.Core.Utilities.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;
    using HintServiceMeow.Core.Enum;
    using HintServiceMeow.Core.Interface;
    using HintServiceMeow.Core.Models;
    using HintServiceMeow.Core.Models.Hints;
    using HintServiceMeow.Core.Models.Parser;
    using HintServiceMeow.Core.Utilities.Pools;
    using HintServiceMeow.Core.Utilities.Tools;

    /// <summary>
    /// Used to parse AbstractHint to rich text message.
    /// </summary>
    internal class HintParser : IHintParser
    {
        private const string PlaceholderTop = "<line-height=0><voffset=9999>P</voffset>";
        private const string PlaceholderBottom = "<line-height=0><voffset=-9999>P</voffset>";

        private static readonly Regex SizeTags = new(RegexPatterns.SizeTagRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AlignmentTags = new(RegexPatterns.AlignTagRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly ICache<Guid, ValueTuple<float, float>> dynamicHintPositionCache;
        private readonly ICoordinateTools coordinateTool;
        private readonly IPool<StringBuilder> stringBuilderPool;
        private readonly IPool<RichTextParser> richTextParserPool;
        private readonly IPool<Hint> hintPool;
        private readonly Dictionary<Hint, HintLayout> fixedLayouts = new();
        private readonly List<TextArea> dynamicHintColliders = new(128);
        private readonly List<HintLayout?> orderedHintGroups = new(128);
        private readonly List<HintLayout> orderedHints = new(128);
        private readonly List<DynamicHint> dynamicHints = new(128);
        private readonly Queue<ValueTuple<float, float>> queue = new();
        private readonly HashSet<ValueTuple<float, float>> visited = new();

        private HintViewport viewport = HintViewport.Default;
        private bool viewportChanged;

        public HintParser(
            ICache<Guid, ValueTuple<float, float>>? dynamicHintPositionCache = null,
            ICoordinateTools? coordinateTool = null,
            IPool<StringBuilder>? stringBuilderPool = null,
            IPool<RichTextParser>? richTextParserPool = null,
            IPool<Hint>? hintPool = null)
        {
            this.dynamicHintPositionCache = dynamicHintPositionCache ?? new Cache<Guid, ValueTuple<float, float>>(500);
            this.coordinateTool = coordinateTool ?? new CoordinateTools();
            this.stringBuilderPool = stringBuilderPool ?? StringBuilderPool.Instance;
            this.richTextParserPool = richTextParserPool ?? RichTextParserPool.Instance;
            this.hintPool = hintPool ?? HintPool.Instance;
        }

        public string ParseToMessage(HintCollection collection) => ParseToMessage(collection, HintViewport.Default);

        internal string ParseToMessage(HintCollection collection, HintViewport playerViewport)
        {
            viewportChanged = viewport.AspectRatio != playerViewport.AspectRatio;
            viewport = playerViewport;
            StringBuilder messageBuilder = stringBuilderPool.Rent();

            try
            {
                IReadOnlyList<IReadOnlyList<AbstractHint>> allGroups = collection.AllGroups;

                // All fixed hints must be fitted before they become dynamic-hint colliders.
                foreach (IReadOnlyList<AbstractHint> group in allGroups)
                {
                    foreach (AbstractHint source in group)
                    {
                        if (source is not Hint { Hide: false } hint || fixedLayouts.ContainsKey(hint))
                            continue;

                        HintLayout? layout = CreateLayout(hint);
                        if (layout is null)
                            continue;

                        fixedLayouts.Add(hint, layout);
                        dynamicHintColliders.Add(layout.Area);
                    }
                }

                foreach (IReadOnlyList<AbstractHint> group in allGroups)
                {
                    foreach (AbstractHint source in group)
                    {
                        if (source is null || source.Hide)
                            continue;

                        if (source is Hint hint && fixedLayouts.TryGetValue(hint, out HintLayout layout))
                            orderedHints.Add(layout);
                        else if (source is DynamicHint dynamicHint)
                            dynamicHints.Add(dynamicHint);
                    }

                    dynamicHints.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                    foreach (DynamicHint hint in dynamicHints)
                    {
                        HintLayout? layout = ParseDynamicHint(hint, dynamicHintColliders);
                        if (layout is null)
                            continue;

                        dynamicHintColliders.Add(layout.Area);
                        orderedHints.Add(layout);
                    }

                    orderedHints.Sort((a, b) => a.Bottom.CompareTo(b.Bottom));
                    orderedHintGroups.AddRange(orderedHints);
                    orderedHintGroups.Add(null);
                    orderedHints.Clear();
                    dynamicHints.Clear();
                }

                messageBuilder.AppendLine(PlaceholderTop);
                foreach (HintLayout? layout in orderedHintGroups)
                {
                    if (layout is null)
                        messageBuilder.AppendLine("</align></size></b></i>");
                    else
                        ParseToRichText(layout, messageBuilder);
                }

                messageBuilder.AppendLine(PlaceholderBottom);
                return messageBuilder.ToString();
            }
            finally
            {
                fixedLayouts.Clear();
                orderedHintGroups.Clear();
                orderedHints.Clear();
                dynamicHints.Clear();
                dynamicHintColliders.Clear();
                queue.Clear();
                visited.Clear();
                stringBuilderPool.Return(messageBuilder);
            }
        }

        private HintLayout? CreateLayout(Hint hint, float horizontalMargin = 0, float verticalMargin = 0)
        {
            string text = RemoveIllegalTags(hint.Content.GetText() ?? string.Empty);
            if (string.IsNullOrEmpty(text))
                return null;

            RichTextParser parser = richTextParserPool.Rent();
            IReadOnlyList<LineInfo> lines;
            try
            {
                lines = parser.ParseText(text, hint.FontSize, hint.Alignment);
            }
            finally
            {
                richTextParserPool.Return(parser);
            }

            float width = 0;
            float height = 0;
            float lineHeight = Math.Max(0, hint.LineHeight);
            foreach (LineInfo line in lines)
            {
                width = Math.Max(width, line.Width);
                height += line.Height + lineHeight;
            }

            height = Math.Max(0, height - lineHeight);
            float scale = 1f;
            if (width > 0)
                scale = Math.Min(scale, Math.Max(1, viewport.Width - horizontalMargin) / width);
            if (height > 0)
                scale = Math.Min(scale, Math.Max(1, viewport.Height - verticalMargin) / height);

            HintLayout layout = new(lines, hint.FontSize * scale, lineHeight * scale, scale, width * scale, height * scale);
            float x = hint.Alignment switch
            {
                HintAlignment.Left => viewport.Left + hint.XCoordinate + (layout.Width / 2),
                HintAlignment.Right => viewport.Right + hint.XCoordinate - (layout.Width / 2),
                _ => hint.XCoordinate,
            };
            float y = coordinateTool.GetYCoordinate(hint.YCoordinate, layout.Height, hint.YCoordinateAlign, HintVerticalAlign.Bottom);
            layout.MoveTo(
                HintViewport.Clamp(x, viewport.Left + (layout.Width / 2), viewport.Right - (layout.Width / 2)),
                HintViewport.Clamp(y, viewport.Top + layout.Height, viewport.Bottom));
            return layout;
        }

        private HintLayout? ParseDynamicHint(DynamicHint source, IList<TextArea> colliders)
        {
            float leftMargin = Math.Max(0, source.LeftMargin);
            float rightMargin = Math.Max(0, source.RightMargin);
            float topMargin = Math.Max(0, source.TopMargin);
            float bottomMargin = Math.Max(0, source.BottomMargin);
            Hint temporaryHint = hintPool.Rent();
            HintLayout? layout;
            try
            {
                temporaryHint.Set(source, source.TargetX, source.TargetY);
                layout = CreateLayout(temporaryHint, leftMargin + rightMargin, topMargin + bottomMargin);
            }
            finally
            {
                hintPool.Return(temporaryHint);
            }

            if (layout is null)
                return null;

            if (viewportChanged)
                dynamicHintPositionCache.TryRemove(source.Guid, out _);

            float minimumX = Math.Max(source.LeftBoundary, viewport.Left + (layout.Width / 2) + leftMargin);
            float maximumX = Math.Min(source.RightBoundary, viewport.Right - (layout.Width / 2) - rightMargin);
            float minimumY = Math.Max(source.TopBoundary, viewport.Top + layout.Height + topMargin);
            float maximumY = Math.Min(source.BottomBoundary, viewport.Bottom - bottomMargin);

            if (minimumX <= maximumX && minimumY <= maximumY)
            {
                ValueTuple<float, float> target = ValueTuple.Create(
                    HintViewport.Clamp(source.TargetX, minimumX, maximumX),
                    HintViewport.Clamp(source.TargetY, minimumY, maximumY));

                if (IsAvailable(target))
                {
                    dynamicHintPositionCache.TryRemove(source.Guid, out _);
                    layout.MoveTo(target.Item1, target.Item2);
                    return layout;
                }

                if (dynamicHintPositionCache.TryGet(source.Guid, out ValueTuple<float, float> cached) && IsAvailable(cached))
                {
                    layout.MoveTo(cached.Item1, cached.Item2);
                    return layout;
                }

                queue.Clear();
                visited.Clear();
                queue.Enqueue(target);
                while (queue.TryDequeue(out ValueTuple<float, float> candidate))
                {
                    if (!visited.Add(candidate))
                        continue;

                    if (IsAvailable(candidate))
                    {
                        dynamicHintPositionCache.Add(source.Guid, candidate);
                        layout.MoveTo(candidate.Item1, candidate.Item2);
                        return layout;
                    }

                    if (candidate.Item2 < maximumY)
                        queue.Enqueue(ValueTuple.Create(candidate.Item1, Math.Min(maximumY, candidate.Item2 + 10)));
                    if (candidate.Item2 > minimumY)
                        queue.Enqueue(ValueTuple.Create(candidate.Item1, Math.Max(minimumY, candidate.Item2 - 10)));
                    if (candidate.Item1 < maximumX)
                        queue.Enqueue(ValueTuple.Create(Math.Min(maximumX, candidate.Item1 + 50), candidate.Item2));
                    if (candidate.Item1 > minimumX)
                        queue.Enqueue(ValueTuple.Create(Math.Max(minimumX, candidate.Item1 - 50), candidate.Item2));
                }
            }

            dynamicHintPositionCache.TryRemove(source.Guid, out _);

            // CreateLayout already fitted the target to the screen. StayInPosition can
            // overlap other hints, but must never fall back to an off-screen position.
            return source.Strategy == DynamicHintStrategy.StayInPosition ? layout : null;

            bool IsAvailable(ValueTuple<float, float> position)
            {
                if (position.Item1 < minimumX || position.Item1 > maximumX || position.Item2 < minimumY || position.Item2 > maximumY)
                    return false;

                TextArea area = new()
                {
                    Left = position.Item1 - (layout.Width / 2) - leftMargin,
                    Right = position.Item1 + (layout.Width / 2) + rightMargin,
                    Top = position.Item2 - layout.Height - topMargin,
                    Bottom = position.Item2 + bottomMargin,
                };
                foreach (TextArea collider in colliders)
                {
                    if (area.HasIntersection(collider))
                        return false;
                }

                return true;
            }
        }

        private void ParseToRichText(HintLayout layout, StringBuilder messageBuilder)
        {
            float vOffset = 700 - layout.Top + layout.LineHeight;
            messageBuilder.AppendFormat(CultureInfo.InvariantCulture, "<align=center><size={0:0.######}><nobr>", layout.FontSize);
            foreach (LineInfo line in layout.Lines)
            {
                vOffset -= (line.Height * layout.Scale) + layout.LineHeight;
                if (string.IsNullOrEmpty(line.RawText))
                    continue;

                float lineWidth = line.Width * layout.Scale;
                float x = line.Alignment switch
                {
                    HintAlignment.Left => layout.CenterX - ((layout.Width - lineWidth) / 2),
                    HintAlignment.Right => layout.CenterX + ((layout.Width - lineWidth) / 2),
                    _ => layout.CenterX,
                };
                string text = AlignmentTags.Replace(line.RawText, string.Empty);
                text = SizeTags.Replace(text, match =>
                {
                    float size = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    if (match.Groups[2].Value == "%")
                        size *= 40f / 100f; // Match RichTextParser's percentage-size baseline.
                    return string.Format(CultureInfo.InvariantCulture, "<size={0:0.######}>", size * layout.Scale);
                });

                messageBuilder.AppendFormat(CultureInfo.InvariantCulture, "<pos={0:0.######}><line-height=0><voffset={1:0.######}>", x, vOffset);
                messageBuilder.Append(text.TrimEnd('\r', '\n'));
                messageBuilder.AppendLine("</voffset>");
            }

            messageBuilder.Append("</nobr></size></align>");
        }

        private string RemoveIllegalTags(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            // Skip if no tag
            bool needsModification = false;
            int i = 0;
            for (; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '{' || c == '}')
                {
                    needsModification = true;
                    break;
                }

                if (c == '<' &&
                    (StartsWithIgnoreCase(raw, i, "<line-height=") ||
                     StartsWithIgnoreCase(raw, i, "<voffset=") ||
                     StartsWithIgnoreCase(raw, i, "<pos=") ||
                     StartsWithIgnoreCase(raw, i, "</voffset>")))
                {
                    needsModification = true;
                    break;
                }
            }

            if (!needsModification)
                return raw;

            StringBuilder sb = stringBuilderPool.Rent();

            int length = raw.Length;

            i = 0;
            while (i < length)
            {
                char c = raw[i];

                // Remove all { and } since {} are somehow not displayable
                if (c == '{' || c == '}')
                {
                    i++;
                    continue;
                }

                // Remove all illegal tags
                if (c == '<')
                {
                    if (StartsWithIgnoreCase(raw, i, "<line-height=") ||
                        StartsWithIgnoreCase(raw, i, "<voffset=") ||
                        StartsWithIgnoreCase(raw, i, "<pos="))
                    {
                        int closeIndex = raw.IndexOf('>', i);
                        if (closeIndex != -1)
                        {
                            i = closeIndex + 1; // Skip the whole tag
                            continue;
                        }
                    }
                    else if (StartsWithIgnoreCase(raw, i, "</voffset>"))
                    {
                        i += 10; // Skip "</voffset>"
                        continue;
                    }
                }

                // If not illegal, reserve the character
                sb.Append(c);
                i++;
            }

            string result = sb.ToString();
            stringBuilderPool.Return(sb);
            return result;
        }

        private bool StartsWithIgnoreCase(string str, int startIndex, string prefix)
        {
            if (startIndex + prefix.Length > str.Length)
                return false;

            for (int i = 0; i < prefix.Length; i++)
            {
                char c1 = str[startIndex + i];
                char c2 = prefix[i];

                // To lower case
                if (c1 >= 'A' && c1 <= 'Z')
                    c1 = (char)(c1 + 32);
                if (c2 >= 'A' && c2 <= 'Z')
                    c2 = (char)(c2 + 32);

                if (c1 != c2)
                    return false;
            }

            return true;
        }
    }
}