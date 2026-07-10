using System;
using System.Drawing;

namespace WeChatMessageNotifier
{
    internal sealed class DpiMetrics
    {
        private const float TitlePointSize = 11.5F;
        private const float BodyPointSize = 10F;
        private const float ClosePointSize = 12F;

        private DpiMetrics(float scale)
        {
            Scale = scale <= 0 ? 1.0f : scale;
            CardWidth = DpiUtil.ScaleInt(PopupCardControl.CardWidth, Scale);
            CardHeight = DpiUtil.ScaleInt(PopupCardControl.CardHeight, Scale);
            CardSpacing = DpiUtil.ScaleInt(PopupHostForm.CardSpacing, Scale);
            ScreenMargin = DpiUtil.ScaleInt(12, Scale);
            CornerRadius = DpiUtil.ScaleInt(12, Scale);
            AccentWidth = Math.Max(1, DpiUtil.ScaleInt(5, Scale));
            AvoidancePadding = DpiUtil.ScaleInt(16, Scale);

            CloseBounds = ScaleRectangle(new Rectangle(338, 7, 32, 32));
            TitleBounds = ScaleRectangle(new Rectangle(24, 11, 310, 29));
            BodyBounds = ScaleRectangle(new Rectangle(24, 44, 335, 55));

            TitleFontPixels = PointsToPixels(TitlePointSize, Scale);
            BodyFontPixels = PointsToPixels(BodyPointSize, Scale);
            CloseFontPixels = PointsToPixels(ClosePointSize, Scale);
            TitleFontPoints = TitlePointSize;
            BodyFontPoints = BodyPointSize;
            CloseFontPoints = ClosePointSize;
        }

        internal float Scale { get; private set; }
        internal int CardWidth { get; private set; }
        internal int CardHeight { get; private set; }
        internal int CardSpacing { get; private set; }
        internal int ScreenMargin { get; private set; }
        internal int CornerRadius { get; private set; }
        internal int AccentWidth { get; private set; }
        internal int AvoidancePadding { get; private set; }
        internal Rectangle CloseBounds { get; private set; }
        internal Rectangle TitleBounds { get; private set; }
        internal Rectangle BodyBounds { get; private set; }
        internal float TitleFontPixels { get; private set; }
        internal float BodyFontPixels { get; private set; }
        internal float CloseFontPixels { get; private set; }
        internal float TitleFontPoints { get; private set; }
        internal float BodyFontPoints { get; private set; }
        internal float CloseFontPoints { get; private set; }

        internal static DpiMetrics FromScale(float scale)
        {
            return new DpiMetrics(scale);
        }

        private Rectangle ScaleRectangle(Rectangle rectangle)
        {
            return new Rectangle(
                DpiUtil.ScaleInt(rectangle.X, Scale),
                DpiUtil.ScaleInt(rectangle.Y, Scale),
                DpiUtil.ScaleInt(rectangle.Width, Scale),
                DpiUtil.ScaleInt(rectangle.Height, Scale));
        }

        private static float PointsToPixels(float points, float scale)
        {
            return Math.Max(
                1.0f,
                DpiUtil.ScaleFloat(
                    points * DpiUtil.BaselineDpi / 72.0f,
                    scale));
        }
    }
}
