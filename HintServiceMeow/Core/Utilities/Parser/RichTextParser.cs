namespace HintServiceMeow.Core.Utilities.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using HintServiceMeow.Core.Enum;
    using HintServiceMeow.Core.Interface;
    using HintServiceMeow.Core.Models.Parser;
    using HintServiceMeow.Core.Utilities.Tools;

    /// <summary>
    /// Use to calculate the rich text's width.
    /// </summary>
    internal class RichTextParser
    {
        private const float DefaultFontSize = 40f;

        private static readonly ICache<ValueTuple<string, float, HintAlignment>, IReadOnlyList<LineInfo>> Cache = new Cache<ValueTuple<string, float, HintAlignment>, IReadOnlyList<LineInfo>>(1000);

        // Lock
        private readonly object parserLock = new();

        private readonly StringBuilder currentRawLineText = new(100);

        private readonly Stack<float> fontSizeStack = new();
        private readonly List<CaseStyle> caseStyleStack = [];
        private readonly List<ScriptStyle> scriptStyles = [];

        private readonly Stack<HintAlignment> hintAlignmentStack = new();

        // Handling
        private int index;

        // Current line status. Only apply to a single line
        private float pos;
        private float lineHeight = float.MinValue;
        private bool hasLineHeight;

        // Line status
        private HintAlignment currentLineAlignment = HintAlignment.Center; // Used since line alignment apply to entire line but can affect multiple line

        // Character status
        private float vOffset;
        private TextStyle style = TextStyle.Normal;

        public IReadOnlyList<LineInfo> ParseText(string? text, int size = 20, HintAlignment alignment = HintAlignment.Center)
        {
            if (text is null)
                return new List<LineInfo>().AsReadOnly();

            ValueTuple<string, float, HintAlignment> cacheKey = ValueTuple.Create(text, size, alignment);

            // Check cache
            if (Cache.TryGet(cacheKey, out IReadOnlyList<LineInfo> cachedResult))
            {
                return new List<LineInfo>(cachedResult);
            }

            // Replace linebreak
            text = text
                .Replace("<br>", "\n")
                .Replace("\\n", "\n");

            List<LineInfo> lines = [];
            List<CharacterInfo> currentChInfos = [];

            lock (parserLock)
            {
                ClearStatus();

                currentLineAlignment = alignment;
                if (alignment != HintAlignment.Center)
                    hintAlignmentStack.Push(alignment);

                fontSizeStack.Push(size);
                caseStyleStack.Add(CaseStyle.Smallcaps);

                int lastIndex = 0;

                while (index < text.Length)
                {
                    if (lastIndex <= index)
                    {
                        currentRawLineText.Append(text.Substring(lastIndex, index - lastIndex + 1));
                        lastIndex = index + 1;
                    }

                    // Check if the character is the start of a tag
                    if (CheckTag(text))
                    {
                        continue;
                    }

                    float overflowValue = 0; // Temporarily remove overflow detection since it is not working properly

                    // float currentWidth = !currentChInfos.Any() ? 0 : currentChInfos.Sum(x => x.Width);
                    // float overflowValue = _currentLineAlignment switch
                    // {
                    //     HintAlignment.Center => currentWidth / 2 + _pos,
                    //     HintAlignment.Left => -1200 + currentWidth + _pos,
                    //     HintAlignment.Right => 1200 + _pos,
                    //     _ => 0,
                    // };

                    // Try change line
                    if (text[index] == '\n' || overflowValue > 1200)
                    {
                        if (text[index] == '\n')
                            currentChInfos.Add(GetChInfo('\n'));// Add \n if the line break at \n

                        // Create new line info
                        lines.Add(GetLineInfo(currentChInfos, currentLineAlignment));

                        // Clear character list
                        currentChInfos = [];

                        // Set default alignment for next line
                        currentLineAlignment = hintAlignmentStack.Any() ? hintAlignmentStack.Peek() : HintAlignment.Center;

                        // Clear line status
                        ClearLineStatus();

                        // Goto next character
                        index++;

                        continue;
                    }

                    currentChInfos.Add(GetChInfo(text[index]));
                    index++;
                }

                if (lastIndex <= index)
                {
                    int cutLength = text.Length - lastIndex;
                    if (cutLength > 0)
                        currentRawLineText.Append(text.Substring(lastIndex, cutLength));
                }

                if (currentChInfos.Count != 0)
                    lines.Add(GetLineInfo(currentChInfos, currentLineAlignment));
            }

            Cache.Add(cacheKey, new List<LineInfo>(lines).AsReadOnly());

            return new List<LineInfo>(lines).AsReadOnly();
        }

        public void ClearStatus()
        {
            index = 0;
            currentRawLineText.Clear();

            currentLineAlignment = HintAlignment.Center;
            hintAlignmentStack.Clear();

            pos = 0;
            lineHeight = float.MinValue;
            hasLineHeight = false;

            vOffset = 0;
            style = TextStyle.Normal;
            fontSizeStack.Clear();
            caseStyleStack.Clear();
            scriptStyles.Clear();
        }

        private static bool TryParseLineHeight(string tag, out float height)
        {
            Match lineHeightMatch = Regex.Match(tag, RegexPatterns.LineHeightTagRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            if (lineHeightMatch.Success)
            {
                float value = float.Parse(lineHeightMatch.Groups[1].Value);
                string unit = lineHeightMatch.Groups[2].Value;

                height = unit.ToLower() switch
                {
                    "px" => value,
                    "%" => DefaultFontSize * value / 100f,
                    "em" => DefaultFontSize * value,
                    _ => value
                };

                return true;
            }

            height = -1;
            return false;
        }

        private static bool TryParseSize(string tag, out float size)
        {
            Match sizeMatch = Regex.Match(tag, RegexPatterns.SizeTagRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            if (sizeMatch.Success)
            {
                float value = float.Parse(sizeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                string unit = sizeMatch.Groups[2].Value;

                size = unit.ToLower() switch
                {
                    "px" => value,
                    "%" => DefaultFontSize * value / 100f,
                    _ => value,
                };

                return true;
            }

            size = -1;
            return false;
        }

        private static bool TryParsePos(string tag, out float pos)
        {
            Match posMatch = Regex.Match(tag, RegexPatterns.PosTagRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            if (posMatch.Success)
            {
                int value = int.Parse(posMatch.Groups[1].Value);

                // string unit = posMatch.Groups[2].Value;
                // TODO: add unit support
                pos = value;

                return true;
            }

            pos = -1;
            return false;
        }

        private static bool TryParseVOffset(string tag, out float vOffset)
        {
            Match vOffsetMatch = Regex.Match(tag, RegexPatterns.VOffsetTagRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            if (vOffsetMatch.Success)
            {
                int value = int.Parse(vOffsetMatch.Groups[1].Value);

                // string unit = vOffsetMatch.Groups[2].Value;
                // TODO: add unit support
                vOffset = value;

                return true;
            }

            vOffset = -1;
            return false;
        }

        private static bool TryParseAlign(string tag, out HintAlignment align)
        {
            Match match = Regex.Match(tag, RegexPatterns.AlignTagRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            if (match.Success && Enum.TryParse(match.Groups[1].Value, true, out HintAlignment alignment))
            {
                align = alignment;

                return true;
            }

            align = HintAlignment.Center;
            return false;
        }

        private LineInfo GetLineInfo(List<CharacterInfo> chs, HintAlignment align)
        {
            string rawText = currentRawLineText.ToString();
            currentRawLineText.Clear();

            LineInfo line = new(
                chs.AsReadOnly(),
                align,
                lineHeight,
                hasLineHeight,
                pos,
                rawText
                );

            return line;
        }

        private CharacterInfo GetChInfo(char ch)
        {
            // Get size
            float currentFontSize = fontSizeStack.Count > 0 ? fontSizeStack.Peek() : DefaultFontSize;

            // Case style
            switch (caseStyleStack.LastOrDefault())
            {
                case CaseStyle.Lowercase:
                    ch = char.ToLower(ch);
                    break;
                case CaseStyle.Uppercase:
                case CaseStyle.Allcaps:
                    ch = char.ToUpper(ch);
                    break;
                case CaseStyle.Smallcaps:
                default: // Default to smallcap
                    if (char.IsLetter(ch))
                    {
                        currentFontSize *= 0.8f;
                        ch = char.ToUpper(ch);
                    }

                    break;
            }

            // Get height with v-offset
            float chHeight = currentFontSize + vOffset;

            // Get character size
            float chWidth = FontTool.Instance.GetCharWidth(ch, currentFontSize, style);

            // Script style
            if (scriptStyles.Contains(ScriptStyle.Superscript))
            {
                chWidth *= (float)Math.Pow(0.5, scriptStyles.Count(x => x == ScriptStyle.Superscript));
            }

            if (scriptStyles.Contains(ScriptStyle.Subscript))
            {
                chWidth *= (float)Math.Pow(0.5, scriptStyles.Count(x => x == ScriptStyle.Subscript));
            }

            CharacterInfo chInfo = new(
                ch,
                currentFontSize,
                chWidth,
                chHeight,
                vOffset);

            return chInfo;
        }

        private void ClearLineStatus() // Clear status that applies to current line
        {
            pos = 0;
            lineHeight = float.MinValue;
            hasLineHeight = false;
        }

        private bool CheckTag(string text)
        {
            string tag = string.Empty;
            bool isTag = false;

            // Try cut tag
            if (text[index] == '<')
            {
                int tagEndIndex = text.IndexOf('>', index);

                if (tagEndIndex != -1)
                {
                    tag = text.Substring(index, tagEndIndex - index + 1);
                    isTag = Tags.IsTag(tag);

                    if (isTag)
                        index = tagEndIndex + 1; // Move cursor to the end of the tag
                }
            }

            // Try handle tag
            if (tag != string.Empty && isTag)
                TryHandleTag(tag);

            // Return whether the text is the start of the tag
            return tag != string.Empty && isTag;
        }

        private bool TryHandleTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return false;

            tag = tag.ToLower();

            // Handle end tag
            if (tag.StartsWith("</"))
            {
                int i;

                switch (tag)
                {
                    case "</b>":
                        style &= ~TextStyle.Bold;
                        return true;
                    case "</i>":
                        style &= ~TextStyle.Italic;
                        return true;
                    case "</size>":
                        if (fontSizeStack.Count > 0)
                            fontSizeStack.Pop();
                        return true;
                    case "</voffset>":
                        this.vOffset = 0;
                        return true;
                    case "</align>":
                        if (hintAlignmentStack.Count > 0)
                            hintAlignmentStack.Pop();
                        return true;
                    case "</lowercase>":
                        i = caseStyleStack.LastIndexOf(CaseStyle.Lowercase);
                        if (i != -1)
                            caseStyleStack.RemoveAt(i);
                        return true;
                    case "</uppercase>":
                        i = caseStyleStack.LastIndexOf(CaseStyle.Uppercase);
                        if (i != -1)
                            caseStyleStack.RemoveAt(i);
                        return true;
                    case "</allcaps>":
                        i = caseStyleStack.LastIndexOf(CaseStyle.Allcaps);
                        if (i != -1)
                            caseStyleStack.RemoveAt(i);
                        return true;
                    case "</smallcaps>":
                        i = caseStyleStack.LastIndexOf(CaseStyle.Smallcaps);
                        if (i != -1)
                            caseStyleStack.RemoveAt(i);
                        return true;
                    case "</sup>":
                        i = scriptStyles.LastIndexOf(ScriptStyle.Superscript);
                        if (i != -1)
                            scriptStyles.RemoveAt(i);
                        return true;
                    case "</sub>":
                        i = scriptStyles.LastIndexOf(ScriptStyle.Subscript);
                        if (i != -1)
                            scriptStyles.RemoveAt(i);
                        return true;
                }

                return false;
            }

            // Handle start tag
            if (tag.StartsWith("<size") && TryParseSize(tag, out float size))
            {
                fontSizeStack.Push(size);

                return true;
            }

            if (tag.StartsWith("<line-height") && TryParseLineHeight(tag, out float height))
            {
                hasLineHeight = true;
                lineHeight = height;

                return true;
            }

            if (tag.StartsWith("<pos") && TryParsePos(tag, out float linesPos))
            {
                this.pos = linesPos;

                return true;
            }

            if (tag.StartsWith("<voffset") && TryParseVOffset(tag, out float vertOffset))
            {
                this.vOffset = vertOffset;

                return true;
            }

            if (tag.StartsWith("<align") && TryParseAlign(tag, out HintAlignment align))
            {
                hintAlignmentStack.Push(align);
                currentLineAlignment = align;

                return true;
            }

            if (tag == "<b>")
            {
                style |= TextStyle.Bold;

                return true;
            }

            if (tag == "<i>")
            {
                style |= TextStyle.Italic;

                return true;
            }

            if (tag == "<lowercase>")
            {
                caseStyleStack.Add(CaseStyle.Lowercase);

                return true;
            }

            if (tag == "<uppercase>")
            {
                caseStyleStack.Add(CaseStyle.Uppercase);

                return true;
            }

            if (tag == "<allcaps>")
            {
                caseStyleStack.Add(CaseStyle.Allcaps);

                return true;
            }

            if (tag == "<smallcaps>")
            {
                caseStyleStack.Add(CaseStyle.Smallcaps);

                return true;
            }

            if (tag == "<sup>")
            {
                scriptStyles.Add(ScriptStyle.Superscript);

                return true;
            }

            if (tag == "<sub>")
            {
                scriptStyles.Add(ScriptStyle.Subscript);

                return true;
            }

            return false;
        }
    }
}
