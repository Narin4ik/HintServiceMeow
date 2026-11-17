namespace HintServiceMeow.Core.Utilities.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
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

        private readonly ICoordinateTools coordinateTool;
        private readonly IPool<StringBuilder> stringBuilderPool;
        private readonly IPool<RichTextParser> richTextParserPool;
        private readonly IPool<Hint> hintPool;
        private readonly List<Hint> rentedHints = new List<Hint>(128);

        // For ParseToMessage method
        private readonly List<TextArea> dynamicHintColliders = new(128);
        private readonly List<Hint> orderedHintGroups = new(128); // Use Null to seperate groups
        private readonly List<HintSortData> sortBuffer = new(128);
        private readonly List<Hint> orderedHints = new(128);
        private readonly List<DynamicHint> dynamicHints = new(128);

        // For ParseToHint method
        private readonly Queue<ValueTuple<float, float>> queue = new();
        private readonly HashSet<ValueTuple<float, float>> visited = new();

        private ICache<Guid, ValueTuple<float, float>> dynamicHintPositionCache;
        private ScreenLayout? screenLayout;

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

        public string ParseToMessage(HintCollection collection) => ParseToMessage(collection, null);

        internal static string RemoveIllegalTags(string raw)
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

            StringBuilder sb = StringBuilderPool.Instance.Rent();

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
                    if (StartsWithIgnoreCase(raw, i, "<noparse>"))
                    {
                        int end = raw.IndexOf("</noparse>", i + 9, StringComparison.OrdinalIgnoreCase);
                        end = end < 0 ? length : end + 10;
                        sb.Append(raw, i, end - i);
                        i = end;
                        continue;
                    }

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
            StringBuilderPool.Instance.Return(sb);
            return result;
        }

        internal string ParseToMessage(HintCollection collection, ScreenLayout? layout)
        {
            if (!ReferenceEquals(screenLayout, layout))
                dynamicHintPositionCache = new Cache<Guid, ValueTuple<float, float>>(500);
            screenLayout = layout;
            try
            {
                return ParseCore(collection);
            }
            finally
            {
                orderedHintGroups.Clear();
                dynamicHintColliders.Clear();
                orderedHints.Clear();
                dynamicHints.Clear();
                sortBuffer.Clear();
                foreach (Hint rented in rentedHints)
                    hintPool.Return(rented);
                rentedHints.Clear();
            }
        }

        private static bool StartsWithIgnoreCase(string str, int startIndex, string prefix)
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

        private string ParseCore(HintCollection collection)
        {
            IReadOnlyList<IReadOnlyList<AbstractHint>> allGroups = collection.AllGroups;
            if (screenLayout != null)
            {
                var snapshots = new List<IReadOnlyList<AbstractHint>>(allGroups.Count);
                foreach (var group in allGroups)
                {
                    var hints = new List<AbstractHint>(group.Count);
                    foreach (AbstractHint item in group)
                        hints.Add(item is Hint fixedHint && !fixedHint.Hide ? screenLayout.Fit(fixedHint, coordinateTool) : item);
                    snapshots.Add(hints);
                }

                allGroups = snapshots;
            }

            for (int i = 0; i < allGroups.Count; i++)
            {
                for (int j = 0; j < allGroups[i].Count; j++)
                {
                    if (allGroups[i][j] is Hint { Hide: false } hint && !string.IsNullOrEmpty(hint.Content.GetText()))
                        dynamicHintColliders.Add(ParseToArea(hint));
                }
            }

            for (int i = 0; i < allGroups.Count; i++)
            {
                if (allGroups[i].Count == 0)
                {
                    continue; // Don't add empty group
                }

                for (int j = 0; j < allGroups[i].Count; j++)
                {
                    // Filter invisible hints
                    if (allGroups[i][j] is null || allGroups[i][j].Hide || string.IsNullOrEmpty(allGroups[i][j].Content.GetText()))
                        continue;

                    if (allGroups[i][j] is Hint s)
                        orderedHints.Add(s);
                    else if (allGroups[i][j] is DynamicHint d)
                        dynamicHints.Add(d);
                }

                // Convert Dynamic Hint
                Comparison<DynamicHint> dynamicHintPriorityComparer = (a, b) => b.Priority - a.Priority;
                dynamicHints.Sort(dynamicHintPriorityComparer);

                for (int j = 0; j < dynamicHints.Count; j++)
                {
                    Hint? handledDH = ParseToHint(dynamicHints[j], dynamicHintColliders);

                    if (handledDH is null)
                        continue;

                    dynamicHintColliders.Add(ParseToArea(handledDH));
                    orderedHints.Add(handledDH);
                }

                for (int j = 0; j < orderedHints.Count; j++)
                {
                    sortBuffer.Add(new HintSortData(orderedHints[j], coordinateTool.GetYCoordinate(orderedHints[j], HintVerticalAlign.Bottom)));
                }

                // Sort and add to ordered hint groups
                sortBuffer.Sort();

                for (int j = 0; j < sortBuffer.Count; j++)
                {
                    orderedHintGroups.Add(sortBuffer[j].Hint);
                }

                orderedHintGroups.Add(null!);

                // Reset buffers for next group
                orderedHints.Clear();
                dynamicHints.Clear();
                sortBuffer.Clear();
            }

            StringBuilder messageBuilder = stringBuilderPool.Rent();

            try
            {
                messageBuilder.AppendLine(PlaceholderTop); // Place Holder

                for (int i = 0; i < orderedHintGroups.Count; i++)
                {
                    // When a group ends
                    if (orderedHintGroups[i] is null)
                    {
                        messageBuilder.AppendLine("</align></size></b></i>"); // Make sure one group will not affect another group
                        continue;
                    }

                    ParseToRichText(orderedHintGroups[i], messageBuilder);
                }

                messageBuilder.AppendLine(PlaceholderBottom); // Place Holder

                return messageBuilder.ToString();
            }
            finally
            {
                stringBuilderPool.Return(messageBuilder);
            }
        }

        private Hint? ParseToHint(DynamicHint dynamicHint, IList<TextArea> colliders)
        {
            Guid cacheKey = dynamicHint.Guid;
            if (screenLayout != null)
                dynamicHint = screenLayout.Prepare(dynamicHint, coordinateTool);
            float dhWidth = coordinateTool.GetTextWidth(dynamicHint);
            float dhHeight = coordinateTool.GetTextHeight(dynamicHint);

            // Check target position before checking the cache
            ValueTuple<float, float> targetCoordinate = ValueTuple.Create(dynamicHint.TargetX, dynamicHint.TargetY);
            TextArea targetArea = DynamicHintToArea(targetCoordinate);

            bool targetAreaAvailable = IsAvailable(targetCoordinate, targetArea);

            for (int i = 0; i < colliders.Count; i++)
            {
                if (targetArea.HasIntersection(colliders[i]))
                {
                    targetAreaAvailable = false;
                    break;
                }
            }

            if (targetAreaAvailable)
            {
                // Clear previous cached position since the target position is usable again
                dynamicHintPositionCache.TryRemove(cacheKey, out _);

                Hint hint = hintPool.Rent();
                rentedHints.Add(hint);
                hint.Set(dynamicHint, dynamicHint.TargetX, dynamicHint.TargetY);
                return hint;
            }

            targetAreaAvailable = true;
            if (dynamicHintPositionCache.TryGet(cacheKey, out ValueTuple<float, float> cachedPosition))
            {
                TextArea dhArea = DynamicHintToArea(cachedPosition);
                targetAreaAvailable = IsAvailable(cachedPosition, dhArea);

                for (int i = 0; i < colliders.Count; i++)
                {
                    if (dhArea.HasIntersection(colliders[i]))
                    {
                        targetAreaAvailable = false;
                        break;
                    }
                }

                if (targetAreaAvailable)
                {
                    Hint hint = hintPool.Rent();
                    rentedHints.Add(hint);
                    hint.Set(dynamicHint, cachedPosition.Item1, cachedPosition.Item2);
                    return hint;
                }
            }

            // If there's no cached position or cached position is not usable, then find new position
            queue.Clear();
            visited.Clear();

            if (screenLayout == null || (dynamicHint.LeftBoundary <= dynamicHint.RightBoundary && dynamicHint.TopBoundary <= dynamicHint.BottomBoundary))
                queue.Enqueue(targetCoordinate);

            while (queue.TryDequeue(out ValueTuple<float, float> tuple))
            {
                // The tuple represent bottom center coordinate, Item 1: x, Item 2: y
                if (!visited.Add(tuple))
                    continue;

                TextArea dhArea = DynamicHintToArea(tuple);

                targetAreaAvailable = IsAvailable(tuple, dhArea);
                for (int i = 0; i < colliders.Count; i++)
                {
                    if (dhArea.HasIntersection(colliders[i]))
                    {
                        targetAreaAvailable = false;
                        break;
                    }
                }

                if (targetAreaAvailable)
                {
                    dynamicHintPositionCache.Add(cacheKey, tuple);

                    Hint hint = hintPool.Rent();
                    rentedHints.Add(hint);
                    hint.Set(dynamicHint, tuple.Item1, tuple.Item2);
                    return hint;
                }

                if (tuple.Item2 < dynamicHint.BottomBoundary)
                    queue.Enqueue(ValueTuple.Create(tuple.Item1, Math.Min(dynamicHint.BottomBoundary, tuple.Item2 + 10)));
                if (tuple.Item2 > dynamicHint.TopBoundary)
                    queue.Enqueue(ValueTuple.Create(tuple.Item1, Math.Max(dynamicHint.TopBoundary, tuple.Item2 - 10)));
                if (tuple.Item1 < dynamicHint.RightBoundary)
                    queue.Enqueue(ValueTuple.Create(Math.Min(dynamicHint.RightBoundary, tuple.Item1 + 50), tuple.Item2));
                if (tuple.Item1 > dynamicHint.LeftBoundary)
                    queue.Enqueue(ValueTuple.Create(Math.Max(dynamicHint.LeftBoundary, tuple.Item1 - 50), tuple.Item2));
            }

            // Failed to find a position, return according to DynamicHintStrategy
            if (dynamicHint.Strategy == DynamicHintStrategy.StayInPosition)
            {
                Hint hint = hintPool.Rent();
                rentedHints.Add(hint);
                hint.Set(dynamicHint, dynamicHint.TargetX, dynamicHint.TargetY);
                return screenLayout?.Fit(hint, coordinateTool) ?? hint;
            }

            // DynamicHintStrategy.Hide
            return null;

            bool IsAvailable(ValueTuple<float, float> point, TextArea area) =>
                screenLayout == null || (screenLayout.Contains(area)
                && point.Item1 >= dynamicHint.LeftBoundary && point.Item1 <= dynamicHint.RightBoundary
                && point.Item2 >= dynamicHint.TopBoundary && point.Item2 <= dynamicHint.BottomBoundary);

            TextArea DynamicHintToArea(ValueTuple<float, float> tuple) =>
                new()
                {
                    Left = tuple.Item1 - (dhWidth / 2) - dynamicHint.LeftMargin,
                    Right = tuple.Item1 + (dhWidth / 2) + dynamicHint.RightMargin,
                    Top = tuple.Item2 - dhHeight - dynamicHint.TopMargin,
                    Bottom = tuple.Item2 + dynamicHint.BottomMargin,
                };
        }

        private TextArea ParseToArea(Hint hint)
        {
            float xCoordinate = coordinateTool.GetXCoordinateWithAlignment(hint);
            float yCoordinate = coordinateTool.GetYCoordinate(hint, HintVerticalAlign.Bottom);

            float width = coordinateTool.GetTextWidth(hint);
            float height = coordinateTool.GetTextHeight(hint);

            if (screenLayout != null)
            {
                float left = float.MaxValue;
                float right = float.MinValue;
                foreach (LineInfo line in coordinateTool.GetLineInfos(HintTextFormatter.GetText(hint), hint.FontSize, hint.Alignment))
                {
                    float x = screenLayout.FitLineX(hint.XCoordinate, line.Width, line.Alignment);
                    float center = line.Alignment switch
                    {
                        HintAlignment.Left => x - ScreenLayout.TextContainerHalfWidth + (line.Width / 2f),
                        HintAlignment.Right => x + ScreenLayout.TextContainerHalfWidth - (line.Width / 2f),
                        _ => x,
                    };
                    left = Math.Min(left, center - (line.Width / 2f));
                    right = Math.Max(right, center + (line.Width / 2f));
                }

                if (left != float.MaxValue)
                    return new TextArea { Top = yCoordinate - height, Bottom = yCoordinate, Left = left, Right = right };
            }

            return new TextArea
            {
                Top = yCoordinate - height,
                Bottom = yCoordinate,
                Left = xCoordinate - (width / 2),
                Right = xCoordinate + (width / 2),
            };
        }

        private void ParseToRichText(Hint hint, StringBuilder messageBuilder)
        {
            // Remove illegal tags
            string text = RemoveIllegalTags(HintTextFormatter.GetText(hint));

            // Parse into line infos
            RichTextParser parser = richTextParserPool.Rent();
            IReadOnlyList<LineInfo> lineList = parser.ParseText(text, hint.FontSize, hint.Alignment);
            richTextParserPool.Return(parser);

            if (lineList.Count == 0)
                return;

            // Get the bottom y coordinate of first line
            float vOffset =
                700
                - coordinateTool.GetYCoordinate(hint, HintVerticalAlign.Top)// Start at the top of the first line
                + hint.LineHeight;// Add extra line height on top of the first line so that the line height will not be calculated for the first line

            // Add default size/alignment
            messageBuilder.AppendFormat("<size={0}>", hint.FontSize);
            if (hint.Alignment != HintAlignment.Center)
            {
                switch (hint.Alignment)
                {
                    case HintAlignment.Left: messageBuilder.Append("<align=left>"); break;
                    case HintAlignment.Right: messageBuilder.Append("<align=right>"); break;
                }
            }

            for (int i = 0; i < lineList.Count; i++)
            {
                vOffset -= lineList[i].Height + hint.LineHeight; // Move y coordinate to the bottom of the line

                if (string.IsNullOrEmpty(lineList[i].RawText))
                    continue;

                float x = screenLayout?.FitLineX(hint.XCoordinate, lineList[i].Width, lineList[i].Alignment) ?? hint.XCoordinate;
                if (x != 0)
                    messageBuilder.AppendFormat(CultureInfo.InvariantCulture, "<pos={0:0.######}>", x); // X coordinate
                messageBuilder.Append("<line-height=0>"); // Make sure each line will not affect each other's position
                if (vOffset != 0)
                    messageBuilder.AppendFormat(CultureInfo.InvariantCulture, "<voffset={0:0.######}>", vOffset); // Y coordinate

                messageBuilder.Append(lineList[i].RawText); // Content

                if (vOffset != 0)
                    messageBuilder.Append("</voffset>"); // End Y coordinate
                messageBuilder.AppendLine(); // Break line
            }

            // End default alignment/size
            if (hint.Alignment != HintAlignment.Center)
                messageBuilder.Append("</align>");
            messageBuilder.Append("</size>");
        }
    }
}
