namespace HintServiceMeow.Core.Utilities.Parser
{
    internal static class RegexPatterns
    {
        public const string SizeTagRegexPattern = @"<size=(\d+(?:\.\d+)?)(px|%)?>";
        public const string LineHeightTagRegexPattern = @"<line-height=([\d\.]+)(px|%|em)?>";
        public const string PosTagRegexPattern = @"<pos=([+-]?\d+(px)?)>";
        public const string VOffsetTagRegexPattern = @"<voffset=([+-]?\d+(px)?)>";
        public const string AlignTagRegexPattern = @"<align=(left|center|right)>|</align>";
    }
}
