// Small, dependency-free Windows 11 visual primitives for local settings UI.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace WeChatMessageNotifier
{
    internal static class Win11Visual
    {
        // Solid values are used only if the official System Backdrop API is
        // unavailable. The normal Windows 11 path draws light tint surfaces
        // over DWM Mica/Acrylic instead of an opaque white sheet.
        // Used only when the Windows 11 system backdrop cannot be enabled.
        // Settings content uses stable opaque surfaces so native controls can
        // always erase their previous pixels during resize and DPI changes.
        internal static readonly Color WindowBackground = Color.FromArgb(248, 249, 251);
        internal static readonly Color SolidCardBackground = Color.FromArgb(250, 251, 252);
        // Settings cards deliberately use an opaque light surface. Mica is
        // the window background only; an opaque card prevents native Label
        // and TextBox controls from retaining or blending old glyphs.
        internal static readonly Color CardTint = Color.FromArgb(255, 250, 251, 252);
        internal static readonly Color MenuSolidBackground = Color.FromArgb(248, 249, 251);
        internal static readonly Color Border = Color.FromArgb(202, 216, 224, 232);
        internal static readonly Color Title = Color.FromArgb(255, 31, 41, 55);
        internal static readonly Color SecondaryText = Color.FromArgb(255, 100, 116, 139);
        internal static readonly Color Accent = Color.FromArgb(255, 22, 163, 74);
        internal static readonly Color AccentHover = Color.FromArgb(255, 21, 128, 61);
        internal static readonly Font UiFont = CreateUiFont(10.5f, FontStyle.Regular);
        internal static readonly Font UiSmallFont = CreateUiFont(10.0f, FontStyle.Regular);
        internal static readonly Font UiTitleFont = CreateUiFont(14.0f, FontStyle.Bold);
        internal static readonly Font UiCardTitleFont = CreateUiFont(11.0f, FontStyle.Bold);

        private static Font CreateUiFont(float pointSize, FontStyle style)
        {
            try
            {
                return new Font("Microsoft YaHei UI", pointSize, style, GraphicsUnit.Point);
            }
            catch
            {
                return new Font(SystemFonts.MessageBoxFont, style);
            }
        }

        internal static void ApplyWindowStyle(Form form)
        {
            form.BackColor = WindowBackground;
            // A fixed point-size system UI font is evaluated at the active
            // monitor DPI. SystemFonts.MessageBoxFont is often only 9pt and
            // looked undersized on the user's high-resolution display.
            form.Font = UiFont;
        }

        internal static void StyleSecondaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.UseCompatibleTextRendering = true;
            button.BackColor = SolidCardBackground;
            button.ForeColor = Title;
            button.FlatAppearance.BorderColor = Color.FromArgb(255, Border.R, Border.G, Border.B);
            button.FlatAppearance.BorderSize = 1;
        }

        internal static void StylePrimaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.UseCompatibleTextRendering = true;
            button.BackColor = Accent;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = AccentHover;
        }

        // Preserve every control's semantic RGB and only remove any alpha
        // inherited from a themed/DWM composition surface. In particular this
        // keeps SecondaryText gray and healthy/warning status colors intact.
        internal static void EnsureOpaqueText(Control root)
        {
            foreach (Control control in root.Controls)
            {
                control.ForeColor = ForceOpaque(control.ForeColor);

                var link = control as LinkLabel;
                if (link != null)
                {
                    link.LinkColor = ForceOpaque(link.LinkColor);
                    link.ActiveLinkColor = ForceOpaque(link.ActiveLinkColor);
                    link.VisitedLinkColor = ForceOpaque(link.VisitedLinkColor);
                    link.UseCompatibleTextRendering = true;
                }

                var checkBox = control as CheckBox;
                if (checkBox != null)
                {
                    checkBox.UseCompatibleTextRendering = true;
                }

                var button = control as Button;
                if (button != null)
                {
                    button.UseCompatibleTextRendering = true;
                }

                EnsureOpaqueText(control);
            }
        }

        internal static Color ForceOpaque(Color color)
        {
            return color.IsEmpty
                ? color
                : Color.FromArgb(255, color.R, color.G, color.B);
        }

        internal static int ScaleLogical(int logicalPixels, int dpi)
        {
            return Math.Max(1, (int)Math.Round(
                logicalPixels * Math.Max(96, dpi) / 96d));
        }

        internal static GraphicsPath CreateRoundRect(Rectangle rectangle, int radius)
        {
            var path = new GraphicsPath();
            // WinForms can paint a child once while TableLayoutPanel is still
            // assigning it a 1- or 2-pixel temporary bounds rectangle. GDI+
            // rejects arcs whose diameter exceeds that rectangle, producing
            // "Parameter is not valid" and the red error-cross placeholders.
            // Clamp the radius and use a normal rectangle for a degenerate
            // surface so every intermediate layout size is paint-safe.
            if (rectangle.Width <= 1 || rectangle.Height <= 1)
            {
                path.AddRectangle(new Rectangle(
                    rectangle.X,
                    rectangle.Y,
                    Math.Max(1, rectangle.Width),
                    Math.Max(1, rectangle.Height)));
                return path;
            }

            var maximumRadius = Math.Max(0,
                Math.Min(rectangle.Width, rectangle.Height) / 2);
            var safeRadius = Math.Max(0, Math.Min(radius, maximumRadius));
            if (safeRadius == 0)
            {
                path.AddRectangle(rectangle);
                return path;
            }

            var diameter = safeRadius * 2;
            path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // Mica is the official backdrop for settings-style, longer-lived windows.
    // It deliberately does not use legacy AccentPolicy composition.
    internal abstract class Win11BackdropForm : Form
    {
        private bool backdropApplied;
        private bool applyingBackdrop;
        private string backdropScreenName;
        private int lastBackdropAttributeResult;
        private int lastBackdropExtendResult;

        // The settings content area deliberately uses normal WinForms
        // erasure.  DWM owns the non-client/backdrop treatment, but skipping
        // client erasure leaves stale pixels behind whenever a sizeable form
        // is resized or moved to a monitor with another DPI.
        internal static bool UsesStableClientErase
        {
            get { return true; }
        }

        internal static Win11BackdropKind SettingsBackdropKind
        {
            get { return Win11BackdropKind.MainWindow; }
        }

        internal bool BackdropAppliedForTest
        {
            get { return backdropApplied; }
        }

        protected bool BackdropApplied
        {
            get { return backdropApplied; }
        }

        protected int LastBackdropAttributeResult
        {
            get { return lastBackdropAttributeResult; }
        }

        protected int LastBackdropExtendResult
        {
            get { return lastBackdropExtendResult; }
        }

        internal static bool ReappliesBackdropForDpiAndScreenChanges
        {
            get { return true; }
        }

        protected Win11BackdropForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnHandleCreated(EventArgs eventArgs)
        {
            base.OnHandleCreated(eventArgs);
            ReapplyBackdrop();
        }

        protected override void OnPaintBackground(PaintEventArgs eventArgs)
        {
            base.OnPaintBackground(eventArgs);
        }

        protected override void OnShown(EventArgs eventArgs)
        {
            base.OnShown(eventArgs);
            ReapplyBackdrop();
            Win11Visual.EnsureOpaqueText(this);
        }

        protected override void OnDpiChanged(DpiChangedEventArgs eventArgs)
        {
            base.OnDpiChanged(eventArgs);
            ReapplyBackdrop();
        }

        protected override void OnLocationChanged(EventArgs eventArgs)
        {
            base.OnLocationChanged(eventArgs);
            if (!IsHandleCreated)
            {
                return;
            }

            if (applyingBackdrop)
            {
                return;
            }

            var screenName = Screen.FromHandle(Handle).DeviceName;
            if (!string.Equals(backdropScreenName, screenName, StringComparison.Ordinal))
            {
                ReapplyBackdrop();
            }
        }

        // Reapply the official MainWindow/Mica path whenever the form moves
        // to another display or receives a new monitor DPI.  This deliberately
        // avoids legacy AccentPolicy and never alters Opacity/TransparencyKey.
        protected bool ReapplyBackdrop()
        {
            if (!IsHandleCreated || applyingBackdrop)
            {
                return false;
            }

            applyingBackdrop = true;
            try
            {
                // Capture the display before invoking DWM so a later monitor
                // change can reapply the same official backdrop type.
                backdropScreenName = Screen.FromHandle(Handle).DeviceName;
                NativeMethods.TryApplyRoundedWindowCorners(Handle);
                backdropApplied = NativeMethods.TryApplySystemBackdrop(
                    Handle,
                    Win11BackdropKind.MainWindow,
                    out lastBackdropAttributeResult,
                    out lastBackdropExtendResult);
                OnBackdropReapplied(
                    backdropApplied,
                    lastBackdropAttributeResult,
                    lastBackdropExtendResult);
                return backdropApplied;
            }
            finally
            {
                applyingBackdrop = false;
            }
        }

        protected virtual void OnBackdropReapplied(
            bool applied,
            int attributeResult,
            int extendResult)
        {
        }

    }

    // Uses GDI+ rather than TextRenderer on top of a DWM backdrop. The text
    // color is forced to alpha 255 but retains the caller's intended RGB.
    internal sealed class Win11TextLabel : Control
    {
        private ContentAlignment textAlign = ContentAlignment.TopLeft;

        internal static bool UsesGdiPlusTextRendering
        {
            get { return true; }
        }

        internal static bool UsesEmptyBackgroundPaint
        {
            get { return false; }
        }

        internal static bool UsesCurrentDpiMeasurement
        {
            get { return true; }
        }

        internal Win11TextLabel()
        {
            SetStyle(ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
            // Static text on a DWM surface must own a stable background. A
            // transparent child control can leave old glyphs behind after a
            // resize or DPI transition.
            BackColor = Win11Visual.SolidCardBackground;
            ForeColor = Win11Visual.Title;
        }

        public ContentAlignment TextAlign
        {
            get { return textAlign; }
            set
            {
                if (textAlign == value)
                {
                    return;
                }

                textAlign = value;
                Invalidate();
            }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            if (!IsHandleCreated)
            {
                return new Size(1, Math.Max(1, Font.Height));
            }

            using (var graphics = CreateGraphics())
            {
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                var measured = graphics.MeasureString(Text ?? string.Empty, Font);
                return new Size(
                    Math.Max(1, (int)Math.Ceiling(measured.Width)),
                    Math.Max(1, (int)Math.Ceiling(measured.Height)));
            }
        }

        protected override void OnTextChanged(EventArgs eventArgs)
        {
            base.OnTextChanged(eventArgs);
            ResizeToPreferredSize();
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs eventArgs)
        {
            base.OnFontChanged(eventArgs);
            ResizeToPreferredSize();
            Invalidate();
        }

        private void ResizeToPreferredSize()
        {
            if (AutoSize)
            {
                Size = GetPreferredSize(Size.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            var background = BackColor.IsEmpty
                ? Win11Visual.SolidCardBackground
                : Win11Visual.ForceOpaque(BackColor);
            using (var brush = new SolidBrush(background))
            {
                eventArgs.Graphics.FillRectangle(brush, ClientRectangle);
            }

            eventArgs.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (var brush = new SolidBrush(Win11Visual.ForceOpaque(ForeColor)))
            using (var format = CreateStringFormat(textAlign, false))
            {
                eventArgs.Graphics.DrawString(
                    Text ?? string.Empty,
                    Font,
                    brush,
                    ClientRectangle,
                    format);
            }
        }

        private static StringFormat CreateStringFormat(
            ContentAlignment alignment,
            bool ellipsis)
        {
            var format = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Trimming = ellipsis
                    ? StringTrimming.EllipsisCharacter
                    : StringTrimming.None
            };

            switch (alignment)
            {
                case ContentAlignment.TopCenter:
                case ContentAlignment.MiddleCenter:
                case ContentAlignment.BottomCenter:
                    format.Alignment = StringAlignment.Center;
                    break;
                case ContentAlignment.TopRight:
                case ContentAlignment.MiddleRight:
                case ContentAlignment.BottomRight:
                    format.Alignment = StringAlignment.Far;
                    break;
                default:
                    format.Alignment = StringAlignment.Near;
                    break;
            }

            return format;
        }
    }

    internal sealed class Win11CardPanel : Panel
    {
        private Font titleFont;

        internal static bool DrawsSingleSurface
        {
            get { return true; }
        }

        // Settings cards own their static title and description. This keeps
        // the tint and text in one paint pass instead of layering transparent
        // child labels over a DWM client surface.
        internal string CardTitle { get; set; }

        internal string CardDescription { get; set; }

        internal int TextAreaRight { get; set; }

        internal static bool DrawsStaticTextInSinglePass
        {
            get { return true; }
        }

        internal Win11CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint, true);
            // A card is a stable, opaque content surface.  The outer form may
            // retain Mica around it, but a transparent Panel chain makes
            // GDI child controls retain old pixels during a resize.
            BackColor = Win11Visual.CardTint;
            Padding = new Padding(14);
            TextAreaRight = -1;
        }

        protected override void OnPaintBackground(PaintEventArgs eventArgs)
        {
            // Let WinForms repaint the stable parent surface first. This
            // clears pixels outside the rounded card tint on resize and DPI
            // changes without retaining previous glyphs.
            base.OnPaintBackground(eventArgs);
        }

        protected override void OnFontChanged(EventArgs eventArgs)
        {
            base.OnFontChanged(eventArgs);
            if (titleFont != null)
            {
                titleFont.Dispose();
            }
            titleFont = new Font(Font, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rectangle = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                return;
            }

            using (var path = Win11Visual.CreateRoundRect(
                rectangle,
                Win11Visual.ScaleLogical(10, DeviceDpi)))
            using (var brush = new SolidBrush(Win11Visual.CardTint))
            using (var pen = new Pen(Win11Visual.Border))
            {
                eventArgs.Graphics.FillPath(brush, path);
                eventArgs.Graphics.DrawPath(pen, path);
            }

            DrawStaticText(eventArgs.Graphics);
            base.OnPaint(eventArgs);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && titleFont != null)
            {
                titleFont.Dispose();
                titleFont = null;
            }
            base.Dispose(disposing);
        }

        private void DrawStaticText(Graphics graphics)
        {
            if (string.IsNullOrWhiteSpace(CardTitle) &&
                string.IsNullOrWhiteSpace(CardDescription))
            {
                return;
            }

            if (titleFont == null)
            {
                titleFont = new Font(Font, FontStyle.Bold);
            }

            var dpi = DeviceDpi;
            var left = Win11Visual.ScaleLogical(14, dpi);
            var top = Win11Visual.ScaleLogical(12, dpi);
            var titleHeight = Math.Max(
                titleFont.Height + Win11Visual.ScaleLogical(6, dpi),
                Win11Visual.ScaleLogical(27, dpi));
            var descriptionTop = top + titleHeight + Win11Visual.ScaleLogical(2, dpi);
            var right = TextAreaRight > 0
                ? Math.Min(TextAreaRight, Width - Padding.Right)
                : Width - Padding.Right;
            var width = Math.Max(1, right - left);
            var titleBounds = new Rectangle(left, top, width, titleHeight);
            var descriptionBounds = new Rectangle(
                left,
                descriptionTop,
                width,
                Math.Max(1, Height - descriptionTop - Win11Visual.ScaleLogical(12, dpi)));
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (var titleBrush = new SolidBrush(Win11Visual.Title))
            using (var descriptionBrush = new SolidBrush(Win11Visual.SecondaryText))
            using (var titleFormat = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            using (var descriptionFormat = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisWord
            })
            {
                graphics.DrawString(
                    CardTitle ?? string.Empty,
                    Win11Visual.UiCardTitleFont,
                    titleBrush,
                    titleBounds,
                    titleFormat);
                graphics.DrawString(
                    CardDescription ?? string.Empty,
                    Win11Visual.UiSmallFont,
                    descriptionBrush,
                    descriptionBounds,
                    descriptionFormat);
            }
        }
    }

    internal interface IWin11MenuSurface
    {
        bool SystemBackdropApplied { get; }
    }

    // Centralizes menu geometry so a submenu meets the active row instead of
    // inheriting the padded, old-style WinForms submenu gap. It flips only
    // when the preferred right-side position would leave the working area.
    internal static class Win11MenuPlacement
    {
        internal const int ItemWidth = 232;
        internal const int ItemHeight = 34;
        private const int BaseOverlapLogicalPixels = 5;
        private const int MaximumOverlapPixels = 8;

        internal static int GetOverlapPixels(int dpi)
        {
            var normalizedDpi = dpi < 96 ? 96 : dpi;
            return Math.Min(
                MaximumOverlapPixels,
                Math.Max(
                    BaseOverlapLogicalPixels,
                    (int)Math.Round(
                        BaseOverlapLogicalPixels * normalizedDpi / 96d)));
        }

        internal static Point CalculateSubMenuLocation(
            Rectangle parentMenuBounds,
            int itemTop,
            Size childSize,
            Rectangle workingArea,
            int overlapPixels)
        {
            var width = Math.Max(1, childSize.Width);
            var height = Math.Max(1, childSize.Height);
            var overlap = Math.Max(0, overlapPixels);
            var x = parentMenuBounds.Right - overlap;
            if (x + width > workingArea.Right)
            {
                x = parentMenuBounds.Left - width + overlap;
            }

            var y = itemTop;
            y = Math.Max(workingArea.Top, Math.Min(y, workingArea.Bottom - height));
            return new Point(x, y);
        }
    }

    // A submenu owner that explicitly places the child dropdown. WinForms'
    // stock placement takes outer menu padding into account, which leaves a
    // distracting visual gap between our rounded Acrylic surfaces.
    internal sealed class Win11SubMenuItem : ToolStripMenuItem
    {
        private bool placementQueued;

        internal static bool UsesActualWindowBounds
        {
            get { return true; }
        }

        internal static bool UsesDeferredFinalPlacement
        {
            get { return true; }
        }

        internal Win11SubMenuItem(string text)
            : base(text)
        {
            // Avoid the stock "Default" heuristic choosing the left side
            // before our final, bounds-aware placement is applied.
            DropDownDirection = ToolStripDropDownDirection.Right;
        }

        protected override void OnDropDownOpened(EventArgs eventArgs)
        {
            base.OnDropDownOpened(eventArgs);
            QueueFinalDropDownPlacement();
        }

        protected override void OnDropDownHide(EventArgs eventArgs)
        {
            base.OnDropDownHide(eventArgs);
            if (Owner != null)
            {
                Owner.Invalidate(Bounds);
            }
        }

        private void QueueFinalDropDownPlacement()
        {
            if (placementQueued || Owner == null || Owner.IsDisposed)
            {
                return;
            }

            placementQueued = true;
            try
            {
                // Run after both the ToolStrip default layout and DWM window
                // creation. Earlier callbacks are overwritten by WinForms.
                Owner.BeginInvoke((MethodInvoker)delegate
                {
                    placementQueued = false;
                    if (DropDown != null && !DropDown.IsDisposed && DropDown.Visible)
                    {
                        PositionDropDownFromWindowRects();
                    }
                    if (Owner != null && !Owner.IsDisposed)
                    {
                        Owner.Invalidate(Bounds);
                    }
                });
            }
            catch (InvalidOperationException)
            {
                placementQueued = false;
                PositionDropDownFromWindowRects();
            }
        }

        private void PositionDropDownFromWindowRects()
        {
            if (Owner == null || DropDown == null)
            {
                return;
            }

            Rectangle ownerBounds;
            if (!NativeMethods.TryGetWindowBounds(Owner.Handle, out ownerBounds))
            {
                ownerBounds = Owner.Bounds;
            }

            Rectangle childBounds;
            var childSize = NativeMethods.TryGetWindowBounds(
                DropDown.Handle,
                out childBounds)
                ? childBounds.Size
                : DropDown.GetPreferredSize(Size.Empty);
            var itemTopLeft = Owner.PointToScreen(
                new Point(Bounds.Left, Bounds.Top));
            var workingArea = Screen.FromRectangle(ownerBounds).WorkingArea;
            DropDown.Location = Win11MenuPlacement.CalculateSubMenuLocation(
                ownerBounds,
                itemTopLeft.Y,
                childSize,
                workingArea,
                Win11MenuPlacement.GetOverlapPixels(
                    NativeMethods.GetWindowDpiOrDefault(Owner.Handle)));
        }
    }

    // Root tray menu: a transient DWM backdrop corresponds to desktop Acrylic.
    internal sealed class Win11ContextMenuStrip : ContextMenuStrip, IWin11MenuSurface
    {
        private const int WmEraseBackground = 0x0014;
        private bool backdropApplied;

        internal static Win11BackdropKind MenuBackdropKind
        {
            get { return Win11BackdropKind.TransientWindow; }
        }

        internal Win11ContextMenuStrip()
        {
            ShowImageMargin = false;
            BackColor = Win11Visual.MenuSolidBackground;
            ForeColor = Win11Visual.Title;
            Font = SystemFonts.MenuFont;
            // Horizontal padding affects WinForms' submenu anchor point and
            // creates a visible gap. Keep only vertical outer breathing room;
            // item text/hover supplies its own horizontal inset.
            Padding = new Padding(0, 4, 0, 4);
            Renderer = new Win11MenuRenderer(this);
        }

        public bool SystemBackdropApplied
        {
            get { return backdropApplied; }
        }

        protected override void OnHandleCreated(EventArgs eventArgs)
        {
            base.OnHandleCreated(eventArgs);
            backdropApplied = NativeMethods.TryApplySystemBackdrop(
                Handle,
                Win11BackdropKind.TransientWindow);
            NativeMethods.TryApplyRoundedWindowCorners(Handle);
        }

        internal static bool UsesCustomRegion
        {
            get { return false; }
        }

        protected override void OnItemAdded(ToolStripItemEventArgs eventArgs)
        {
            base.OnItemAdded(eventArgs);
            var item = eventArgs.Item as ToolStripMenuItem;
            if (item != null)
            {
                item.AutoSize = false;
                item.Width = Win11MenuPlacement.ItemWidth;
                item.Height = Win11MenuPlacement.ItemHeight;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs eventArgs)
        {
            if (!backdropApplied)
            {
                base.OnPaintBackground(eventArgs);
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (backdropApplied && message.Msg == WmEraseBackground)
            {
                message.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref message);
        }
    }

    // Explicit DropDown type ensures the notification/diagnostic submenus do
    // not silently fall back to the default rectangular WinForms white menu.
    internal sealed class Win11ToolStripDropDownMenu : ToolStripDropDownMenu, IWin11MenuSurface
    {
        private const int WmEraseBackground = 0x0014;
        private bool backdropApplied;

        internal static Win11BackdropKind MenuBackdropKind
        {
            get { return Win11BackdropKind.TransientWindow; }
        }

        internal Win11ToolStripDropDownMenu()
        {
            ShowImageMargin = false;
            BackColor = Win11Visual.MenuSolidBackground;
            ForeColor = Win11Visual.Title;
            Font = SystemFonts.MenuFont;
            Padding = new Padding(0, 4, 0, 4);
            Renderer = new Win11MenuRenderer(this);
        }

        public bool SystemBackdropApplied
        {
            get { return backdropApplied; }
        }

        protected override void OnHandleCreated(EventArgs eventArgs)
        {
            base.OnHandleCreated(eventArgs);
            backdropApplied = NativeMethods.TryApplySystemBackdrop(
                Handle,
                Win11BackdropKind.TransientWindow);
            NativeMethods.TryApplyRoundedWindowCorners(Handle);
        }

        internal static bool UsesCustomRegion
        {
            get { return false; }
        }

        protected override void OnItemAdded(ToolStripItemEventArgs eventArgs)
        {
            base.OnItemAdded(eventArgs);
            var item = eventArgs.Item as ToolStripMenuItem;
            if (item != null)
            {
                item.AutoSize = false;
                item.Width = Win11MenuPlacement.ItemWidth;
                item.Height = Win11MenuPlacement.ItemHeight;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs eventArgs)
        {
            if (!backdropApplied)
            {
                base.OnPaintBackground(eventArgs);
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (backdropApplied && message.Msg == WmEraseBackground)
            {
                message.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref message);
        }
    }

    internal sealed class Win11MenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly IWin11MenuSurface owner;

        internal static bool UsesGdiPlusTextRendering
        {
            get { return true; }
        }

        internal static bool SkipsSolidBackgroundWhenBackdropApplied
        {
            get { return true; }
        }

        internal Win11MenuRenderer(IWin11MenuSurface owner)
        {
            this.owner = owner;
        }

        protected override void OnRenderToolStripBackground(
            ToolStripRenderEventArgs eventArgs)
        {
            // TransientWindow is the official Desktop Acrylic backdrop. A
            // GDI FillRectangle here would cover that whole surface and make
            // the menu look like an opaque white WinForms popup.
            if (owner.SystemBackdropApplied)
            {
                return;
            }

            using (var brush = new SolidBrush(Win11Visual.MenuSolidBackground))
            {
                eventArgs.Graphics.FillRectangle(brush, eventArgs.AffectedBounds);
            }
        }

        protected override void OnRenderMenuItemBackground(
            ToolStripItemRenderEventArgs eventArgs)
        {
            var menuItem = eventArgs.Item as ToolStripMenuItem;
            var childOpen = menuItem != null && menuItem.DropDown.Visible;
            if (!eventArgs.Item.Selected && !eventArgs.Item.Pressed && !childOpen)
            {
                return;
            }

            var bounds = eventArgs.Item.ContentRectangle;
            bounds.Inflate(-3, -1);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            using (var path = Win11Visual.CreateRoundRect(bounds, 7))
            using (var brush = new SolidBrush(Color.FromArgb(170, 226, 239, 246)))
            {
                eventArgs.Graphics.FillPath(brush, path);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs eventArgs)
        {
            var color = eventArgs.Item.Enabled
                ? Win11Visual.Title
                : Win11Visual.SecondaryText;
            eventArgs.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (var brush = new SolidBrush(Win11Visual.ForceOpaque(color)))
            using (var format = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                eventArgs.Graphics.DrawString(
                    eventArgs.Text ?? string.Empty,
                    eventArgs.TextFont,
                    brush,
                    GetTextBounds(eventArgs.Item),
                    format);
            }

            var checkedItem = eventArgs.Item as ToolStripMenuItem;
            if (checkedItem != null && checkedItem.Checked)
            {
                DrawCheck(eventArgs.Graphics, eventArgs.Item.ContentRectangle);
            }
        }

        private static Rectangle GetTextBounds(ToolStripItem item)
        {
            var bounds = item.ContentRectangle;
            bounds.X += 9;
            bounds.Width = Math.Max(1, bounds.Width - 27);
            return bounds;
        }

        private static void DrawCheck(Graphics graphics, Rectangle bounds)
        {
            var x = bounds.Right - 17;
            var y = bounds.Top + (bounds.Height / 2);
            using (var pen = new Pen(Win11Visual.Accent, 2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                graphics.DrawLines(pen, new[]
                {
                    new Point(x, y),
                    new Point(x + 3, y + 3),
                    new Point(x + 9, y - 4)
                });
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs eventArgs)
        {
            eventArgs.ArrowColor = Win11Visual.Title;
            base.OnRenderArrow(eventArgs);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs eventArgs)
        {
            var bounds = eventArgs.Item.ContentRectangle;
            using (var pen = new Pen(Win11Visual.Border))
            {
                eventArgs.Graphics.DrawLine(
                    pen,
                    bounds.Left + 7,
                    bounds.Top + (bounds.Height / 2),
                    bounds.Right - 7,
                    bounds.Top + (bounds.Height / 2));
            }
        }
    }
}
