using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WeChatMessageNotifier
{
    internal sealed class PopupCardControl : UserControl
    {
        internal const int AutoCloseMilliseconds = 2 * 60 * 1000;
        internal const int CardWidth = 380;
        internal const int CardHeight = 118;
        private static readonly Color SolidHostBackgroundColor = Color.White;
        private static readonly Color SolidCardBackgroundColor =
            Color.FromArgb(250, 252, 255);
        private static readonly Color SolidCardBorderColor =
            Color.FromArgb(226, 232, 240);
        private static readonly Color GlassHostBackgroundColor =
            Color.FromArgb(244, 247, 250);
        private static readonly Color GlassCardBackgroundColor =
            Color.FromArgb(240, 248, 251, 254);
        private static readonly Color GlassCardBorderColor =
            Color.FromArgb(180, 218, 226, 235);
        private static readonly Color AccentColor =
            Color.FromArgb(7, 193, 96);
        private static readonly Color TitleColor =
            Color.FromArgb(255, 31, 41, 55);
        private static readonly Color BodyColor =
            Color.FromArgb(255, 75, 85, 99);
        private static readonly Color CloseColor =
            Color.FromArgb(255, 75, 85, 99);

        private readonly Timer autoCloseTimer;
        private Font titleFont;
        private Font bodyFont;
        private Font closeFont;
        private DpiMetrics metrics;
        private PopupVisualMode visualMode;
        private double visualOpacity = 1;
        private double visualScale = 1;

        internal static bool DiagnosticBlackTextEnabled { get; set; }

        internal PopupCardControl(PopupEntry entry)
            : this(entry, 1.0f, PopupVisualMode.Glass)
        {
        }

        internal PopupCardControl(PopupEntry entry, float dpiScale)
            : this(entry, dpiScale, PopupVisualMode.Glass)
        {
        }

        internal PopupCardControl(
            PopupEntry entry,
            float dpiScale,
            PopupVisualMode visualMode)
        {
            metrics = DpiMetrics.FromScale(dpiScale);
            this.visualMode = visualMode;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.ResizeRedraw,
                true);
            Width = metrics.CardWidth;
            Height = metrics.CardHeight;
            BackColor = visualMode == PopupVisualMode.Glass
                ? Color.Transparent
                : GetHostBackgroundColor();
            Cursor = Cursors.Hand;

            RebuildFonts();
            autoCloseTimer = new Timer
            {
                Interval = AutoCloseMilliseconds
            };
            autoCloseTimer.Tick += delegate
            {
                autoCloseTimer.Stop();
                OnCloseRequested();
            };

            UpdateEntry(entry, true);
        }

        internal event EventHandler CardClicked;
        internal event EventHandler CloseRequested;
        internal PopupEntry Entry { get; private set; }

        internal double VisualOpacity
        {
            get { return visualOpacity; }
        }

        internal double VisualScale
        {
            get { return visualScale; }
        }

        internal float DpiScale
        {
            get { return metrics.Scale; }
        }

        internal float TitleFontPixels
        {
            get { return metrics.TitleFontPixels; }
        }

        internal float TitleFontPoints
        {
            get { return metrics.TitleFontPoints; }
        }

        internal float BodyFontPoints
        {
            get { return metrics.BodyFontPoints; }
        }

        internal GraphicsUnit FontUnit
        {
            get { return titleFont == null ? GraphicsUnit.Point : titleFont.Unit; }
        }

        internal bool UsesDirectTextRendering
        {
            get { return true; }
        }

        internal bool UsesLayeredTransparency
        {
            get { return false; }
        }

        internal PopupVisualMode VisualMode
        {
            get { return visualMode; }
        }

        internal int CardTintAlpha
        {
            get { return GetCardBackgroundColor().A; }
        }

        internal bool TextColorsAreOpaque
        {
            get
            {
                return TitleColor.A == 255 &&
                       BodyColor.A == 255 &&
                       CloseColor.A == 255;
            }
        }

        internal Color TitleTextColor
        {
            get { return TitleColor; }
        }

        internal Color BodyTextColor
        {
            get { return BodyColor; }
        }

        internal Color CloseTextColor
        {
            get { return CloseColor; }
        }

        internal bool ClearsOpaqueWhiteBackground
        {
            get { return visualMode == PopupVisualMode.Solid; }
        }

        internal bool DiagnosticBlackTextDrawsLast
        {
            get { return true; }
        }

        internal void UpdateEntry(
            PopupEntry entry,
            bool resetLifetime)
        {
            Entry = entry;
            if (resetLifetime)
            {
                autoCloseTimer.Stop();
                autoCloseTimer.Start();
            }
            Invalidate();
        }

        internal void ApplyVisual(double opacity, double scale)
        {
            opacity = Math.Max(0, Math.Min(1, opacity));
            // Scale remains in the animation state so existing motion tests
            // and future non-text effects can use it, but text is never drawn
            // through a scaled bitmap. Scaling ClearType text is the main
            // reason the popup looked soft during enter/update animations.
            scale = Math.Max(0.98, Math.Min(1.02, scale));
            if (Math.Abs(opacity - visualOpacity) < 0.005 &&
                Math.Abs(scale - visualScale) < 0.002)
            {
                return;
            }

            visualOpacity = opacity;
            visualScale = scale;
            Visible = opacity > 0.005;
            Invalidate();
        }

        internal void UpdateDpiScale(float dpiScale)
        {
            if (Math.Abs(metrics.Scale - dpiScale) < 0.01f)
            {
                return;
            }

            metrics = DpiMetrics.FromScale(dpiScale);
            Size = new Size(metrics.CardWidth, metrics.CardHeight);
            RebuildFonts();
            Invalidate();
        }

        internal void UpdateVisualMode(PopupVisualMode visualMode)
        {
            if (this.visualMode == visualMode)
            {
                return;
            }

            this.visualMode = visualMode;
            BackColor = visualMode == PopupVisualMode.Glass
                ? Color.Transparent
                : GetHostBackgroundColor();
            Invalidate();
        }

        internal void StopLifetime()
        {
            autoCloseTimer.Stop();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            var hostBackground = GetHostBackgroundColor();
            var cardBackground = GetCardBackgroundColor();
            var cardBorder = GetCardBorderColor();
            // In Glass mode PopupHostForm owns the one and only background
            // composition. This control draws foreground text and hit targets
            // only, preventing a second tint/clear layer under every card.
            if (visualMode == PopupVisualMode.Solid)
            {
                eventArgs.Graphics.Clear(hostBackground);
            }
            if (visualOpacity <= 0.005)
            {
                return;
            }

            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            eventArgs.Graphics.PixelOffsetMode =
                PixelOffsetMode.Half;
            eventArgs.Graphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var opacity = Math.Max(0, Math.Min(1, visualOpacity));
            using (var path = CreateRoundedPath(
                new Rectangle(
                    0,
                    0,
                    metrics.CardWidth - 1,
                metrics.CardHeight - 1),
                metrics.CornerRadius))
            using (var background = new SolidBrush(
                GetPaintColor(hostBackground, cardBackground, opacity)))
            using (var accent = new SolidBrush(
                GetPaintColor(hostBackground, AccentColor, opacity)))
            using (var border = new Pen(
                GetPaintColor(hostBackground, cardBorder, opacity),
                Math.Max(1, DpiUtil.ScaleInt(1, metrics.Scale))))
            {
                if (visualMode == PopupVisualMode.Solid)
                {
                    eventArgs.Graphics.FillPath(background, path);
                    eventArgs.Graphics.SetClip(path);
                    eventArgs.Graphics.FillRectangle(
                        accent,
                        0,
                        0,
                        metrics.AccentWidth,
                        metrics.CardHeight);
                    eventArgs.Graphics.ResetClip();
                    eventArgs.Graphics.DrawPath(border, path);
                }
            }

            TextRenderer.DrawText(
                eventArgs.Graphics,
                Entry.TitleText,
                titleFont,
                metrics.TitleBounds,
                TitleColor,
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.SingleLine |
                TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(
                eventArgs.Graphics,
                Entry.BodyText,
                bodyFont,
                metrics.BodyBounds,
                BodyColor,
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.WordBreak);
            TextRenderer.DrawText(
                eventArgs.Graphics,
                "\u00D7",
                closeFont,
                metrics.CloseBounds,
                CloseColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.SingleLine);

            if (DiagnosticBlackTextEnabled)
            {
                TextRenderer.DrawText(
                    eventArgs.Graphics,
                    "DIAG BLACK TEXT",
                    titleFont,
                    new Rectangle(
                        DpiUtil.ScaleInt(20, metrics.Scale),
                        DpiUtil.ScaleInt(20, metrics.Scale),
                        DpiUtil.ScaleInt(240, metrics.Scale),
                        DpiUtil.ScaleInt(30, metrics.Scale)),
                    Color.Black,
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.NoClipping |
                    TextFormatFlags.SingleLine);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs eventArgs)
        {
            if (visualMode == PopupVisualMode.Solid)
            {
                base.OnPaintBackground(eventArgs);
            }
            // Glass background is painted exactly once by PopupHostForm.
        }

        protected override void OnMouseClick(MouseEventArgs eventArgs)
        {
            base.OnMouseClick(eventArgs);
            if (metrics.CloseBounds.Contains(eventArgs.Location))
            {
                autoCloseTimer.Stop();
                OnCloseRequested();
                return;
            }

            autoCloseTimer.Stop();
            var handler = CardClicked;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                autoCloseTimer.Dispose();
                DisposeFonts();
            }
            base.Dispose(disposing);
        }

        private void RebuildFonts()
        {
            DisposeFonts();
            titleFont = new Font(
                "Microsoft YaHei UI",
                metrics.TitleFontPoints,
                FontStyle.Bold,
                GraphicsUnit.Point);
            bodyFont = new Font(
                "Microsoft YaHei UI",
                metrics.BodyFontPoints,
                FontStyle.Regular,
                GraphicsUnit.Point);
            closeFont = new Font(
                "Microsoft YaHei UI",
                metrics.CloseFontPoints,
                FontStyle.Regular,
                GraphicsUnit.Point);
        }

        private void DisposeFonts()
        {
            if (titleFont != null)
            {
                titleFont.Dispose();
                titleFont = null;
            }
            if (bodyFont != null)
            {
                bodyFont.Dispose();
                bodyFont = null;
            }
            if (closeFont != null)
            {
                closeFont.Dispose();
                closeFont = null;
            }
        }

        private static Color BlendColor(
            Color background,
            Color foreground,
            double opacity)
        {
            opacity = Math.Max(0, Math.Min(1, opacity));
            return Color.FromArgb(
                background.R + (int)Math.Round((foreground.R - background.R) * opacity),
                background.G + (int)Math.Round((foreground.G - background.G) * opacity),
                background.B + (int)Math.Round((foreground.B - background.B) * opacity));
        }

        private Color GetPaintColor(
            Color background,
            Color foreground,
            double opacity)
        {
            opacity = Math.Max(0, Math.Min(1, opacity));
            if (visualMode == PopupVisualMode.Glass)
            {
                return Color.FromArgb(
                    (int)Math.Round(foreground.A * opacity),
                    foreground.R,
                    foreground.G,
                    foreground.B);
            }
            return BlendColor(background, foreground, opacity);
        }

        private Color GetHostBackgroundColor()
        {
            return visualMode == PopupVisualMode.Glass
                ? GlassHostBackgroundColor
                : SolidHostBackgroundColor;
        }

        private Color GetCardBackgroundColor()
        {
            return visualMode == PopupVisualMode.Glass
                ? GlassCardBackgroundColor
                : SolidCardBackgroundColor;
        }

        private Color GetCardBorderColor()
        {
            return visualMode == PopupVisualMode.Glass
                ? GlassCardBorderColor
                : SolidCardBorderColor;
        }

        private static GraphicsPath CreateRoundedPath(
            Rectangle rectangle,
            int radius)
        {
            var diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(
                rectangle.Left,
                rectangle.Top,
                diameter,
                diameter,
                180,
                90);
            path.AddArc(
                rectangle.Right - diameter,
                rectangle.Top,
                diameter,
                diameter,
                270,
                90);
            path.AddArc(
                rectangle.Right - diameter,
                rectangle.Bottom - diameter,
                diameter,
                diameter,
                0,
                90);
            path.AddArc(
                rectangle.Left,
                rectangle.Bottom - diameter,
                diameter,
                diameter,
                90,
                90);
            path.CloseFigure();
            return path;
        }

        private void OnCloseRequested()
        {
            var handler = CloseRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
