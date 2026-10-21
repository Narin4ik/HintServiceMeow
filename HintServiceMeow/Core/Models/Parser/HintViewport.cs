namespace HintServiceMeow.Core.Models.Parser
{
    using System;

    /// <summary>
    /// An immutable snapshot of the player's visible area in hint canvas units.
    /// </summary>
    internal sealed class HintViewport
    {
        public const float CanvasHeight = 1080f;
        public const float VerticalPadding = 16f;
        public const float DefaultAspectRatio = 16f / 9f;

        public HintViewport(float aspectRatio)
        {
            AspectRatio = IsValidAspectRatio(aspectRatio) ? aspectRatio : DefaultAspectRatio;
        }

        public static HintViewport Default { get; } = new(DefaultAspectRatio);

        public float AspectRatio { get; }

        public float Left => -(CanvasHeight * AspectRatio / 2f);

        public float Right => CanvasHeight * AspectRatio / 2f;

        public float Top => VerticalPadding;

        public float Bottom => CanvasHeight - VerticalPadding;

        public float Width => Right - Left;

        public float Height => Bottom - Top;

        public static bool IsValidAspectRatio(float value) =>
            value >= 1f && !float.IsInfinity(value * CanvasHeight);

        public static float Clamp(float value, float minimum, float maximum) =>
            Math.Max(minimum, Math.Min(maximum, value));
    }
}
