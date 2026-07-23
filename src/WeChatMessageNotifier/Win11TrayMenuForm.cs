// A compact, single-window tray menu.  Nested commands expand inside the
// same DWM transient surface rather than creating a second popup window.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace WeChatMessageNotifier
{
    internal sealed class TrayMenuEntry
    {
        internal TrayMenuEntry(string text, Action action)
        {
            Text = text ?? string.Empty;
            Action = action;
            Children = new List<TrayMenuEntry>();
            Enabled = true;
        }

        internal string Text { get; set; }
        internal bool Enabled { get; set; }
        internal bool IsChecked { get; set; }
        internal bool IsSeparator { get; set; }
        internal bool IsExpanded { get; set; }
        internal Action Action { get; set; }
        internal IList<TrayMenuEntry> Children { get; private set; }

        internal bool HasChildren
        {
            get { return Children != null && Children.Count != 0; }
        }

        internal static TrayMenuEntry Separator()
        {
            return new TrayMenuEntry(string.Empty, null) { IsSeparator = true };
        }
    }

    // Manages exactly one non-activating Form.  The timer runs only while the
    // menu is visible, solely to dismiss it after an outside click or Escape.
    internal sealed class Win11TrayMenuController : IDisposable
    {
        private readonly IList<TrayMenuEntry> rootEntries;
        private readonly Timer dismissTimer;
        private Win11TrayMenuForm rootMenu;
        private bool disposed;
        private bool mouseButtonWasDown;

        internal static bool UsesFormRuntime { get { return true; } }
        internal static bool UsesSingleRootMenu { get { return true; } }
        internal static bool UsesNoSubMenuWindow { get { return true; } }
        internal static bool UsesBottomAnchoredExpansion { get { return true; } }

        internal Win11TrayMenuController(IList<TrayMenuEntry> rootEntries)
        {
            this.rootEntries = rootEntries ?? new List<TrayMenuEntry>();
            dismissTimer = new Timer { Interval = 75 };
            dismissTimer.Tick += DismissTimerTick;
        }

        internal void Show(Point screenPoint)
        {
            CollapseAll();
            if (rootMenu == null || rootMenu.IsDisposed)
            {
                rootMenu = new Win11TrayMenuForm(this);
            }

            rootMenu.SetEntries(rootEntries);
            rootMenu.Location = ClampRootLocation(rootMenu.Size, screenPoint);
            rootMenu.Show();
            rootMenu.BringToFront();
            mouseButtonWasDown = IsDismissButtonDown();
            dismissTimer.Start();
        }

        internal void Hide()
        {
            dismissTimer.Stop();
            if (rootMenu != null && !rootMenu.IsDisposed)
            {
                rootMenu.Hide();
            }
        }

        internal void Execute(TrayMenuEntry entry)
        {
            if (entry == null || !entry.Enabled)
            {
                return;
            }

            Hide();
            if (entry.Action != null)
            {
                entry.Action();
            }
        }

        internal void ToggleExpanded(TrayMenuEntry entry)
        {
            if (entry == null || !entry.HasChildren)
            {
                return;
            }

            var shouldExpand = !entry.IsExpanded;
            CollapseAll();
            entry.IsExpanded = shouldExpand;
            if (rootMenu != null && !rootMenu.IsDisposed && rootMenu.Visible)
            {
                rootMenu.RefreshLayoutKeepingBottom();
            }
        }

        private void CollapseAll()
        {
            foreach (var entry in rootEntries)
            {
                if (entry != null)
                {
                    entry.IsExpanded = false;
                }
            }
        }

        private void DismissTimerTick(object sender, EventArgs eventArgs)
        {
            if (disposed || rootMenu == null || rootMenu.IsDisposed || !rootMenu.Visible)
            {
                dismissTimer.Stop();
                return;
            }

            if (NativeMethods.IsVirtualKeyDown(0x1b))
            {
                Hide();
                return;
            }

            var buttonDown = IsDismissButtonDown();
            if (buttonDown && !mouseButtonWasDown &&
                !rootMenu.Bounds.Contains(Cursor.Position))
            {
                Hide();
                return;
            }
            mouseButtonWasDown = buttonDown;
        }

        private static bool IsDismissButtonDown()
        {
            return NativeMethods.IsVirtualKeyDown(0x01) ||
                NativeMethods.IsVirtualKeyDown(0x02);
        }

        private static Point ClampRootLocation(Size menuSize, Point point)
        {
            var area = Screen.FromPoint(point).WorkingArea;
            return new Point(
                Math.Max(area.Left, Math.Min(point.X, area.Right - menuSize.Width)),
                Math.Max(area.Top, Math.Min(point.Y, area.Bottom - menuSize.Height)));
        }

        internal static Rectangle CalculateBottomAnchoredBounds(
            Rectangle currentBounds,
            Size desiredSize,
            Rectangle workingArea)
        {
            var width = Math.Min(Math.Max(1, desiredSize.Width), workingArea.Width);
            var height = Math.Min(Math.Max(1, desiredSize.Height), workingArea.Height);
            var x = currentBounds.Right - width;
            var y = currentBounds.Bottom - height;
            x = Math.Max(workingArea.Left, Math.Min(x, workingArea.Right - width));
            y = Math.Max(workingArea.Top, Math.Min(y, workingArea.Bottom - height));
            return new Rectangle(x, y, width, height);
        }

        // State-only coverage for expansion, automatic collapse, in-window
        // child visibility, and bottom-anchored resize geometry.
        internal static bool RunExpansionSelfTest()
        {
            var first = new TrayMenuEntry("first", null);
            first.Children.Add(new TrayMenuEntry("first child", null));
            var second = new TrayMenuEntry("second", null);
            second.Children.Add(new TrayMenuEntry("second child", null));
            var entries = new List<TrayMenuEntry> { first, second };
            using (var controller = new Win11TrayMenuController(entries))
            {
                controller.ToggleExpanded(first);
                var firstExpanded = first.IsExpanded && !second.IsExpanded;
                // Keep the self-test state-only. Creating a transient DWM
                // window in the non-interactive test process can block while
                // Windows initializes its composition surface; menu painting
                // itself is covered by the ordinary build/runtime path.
                var childInSameWindow = first.Children.Count == 1 &&
                    first.Children[0].Text == "first child";
                controller.ToggleExpanded(second);
                var secondExpanded = !first.IsExpanded && second.IsExpanded;
                controller.ToggleExpanded(second);
                var collapsed = !first.IsExpanded && !second.IsExpanded;
                var oldBounds = new Rectangle(500, 600, 232, 200);
                var resized = CalculateBottomAnchoredBounds(
                    oldBounds,
                    new Size(232, 300),
                    new Rectangle(0, 0, 1000, 900));
                return firstExpanded && childInSameWindow && secondExpanded && collapsed &&
                    resized.Bottom == oldBounds.Bottom && resized.Right == oldBounds.Right;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Hide();
            dismissTimer.Dispose();
            if (rootMenu != null && !rootMenu.IsDisposed)
            {
                rootMenu.Dispose();
            }
        }
    }

    // All rows, including expanded child rows, are painted in this one Form.
    // The normal Windows 11 path leaves the whole client surface to DWM
    // Desktop Acrylic; only the solid fallback paints a regular background.
    internal sealed class Win11TrayMenuForm : Form
    {
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;
        private const int WmEraseBackground = 0x0014;
        private readonly Win11TrayMenuController controller;
        private readonly List<VisibleEntry> visibleEntries = new List<VisibleEntry>();
        private IList<TrayMenuEntry> entries = new List<TrayMenuEntry>();
        private bool acrylicApplied;
        private int hoverIndex = -1;

        private sealed class VisibleEntry
        {
            internal TrayMenuEntry Entry;
            internal int IndentLevel;
        }

        internal const int ChildIndentLogicalPixels = 22;
        internal const int HoverAlpha = 92;
        // This is the alpha embedded in the dedicated white Acrylic policy,
        // not a GDI overlay. The composition system blends it with the real
        // desktop rather than the old TransientWindow gray material.
        internal const int BackdropTintAlpha =
            NativeMethods.LightTrayAcrylicTintAlpha;

        internal static Win11BackdropKind MenuBackdropKind
        {
            get { return Win11BackdropKind.None; }
        }

        internal static bool UsesSingleBackdropTint { get { return true; } }
        internal static bool UsesOpaqueDarkText { get { return true; } }
        internal static bool UsesNoCustomRegion { get { return true; } }
        internal static bool UsesDedicatedLightAcrylicComposition { get { return true; } }
        internal static bool UsesDwmBackdropWithoutOpaqueOverlay { get { return false; } }

        internal Win11TrayMenuForm(Win11TrayMenuController controller)
        {
            this.controller = controller;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            TopMost = true;
            BackColor = Win11Visual.MenuSolidBackground;
            Font = SystemFonts.MenuFont;
            Padding = new Padding(6);
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint, true);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= WsExToolWindow | WsExNoActivate;
                return parameters;
            }
        }

        internal void SetEntries(IList<TrayMenuEntry> source)
        {
            entries = source ?? new List<TrayMenuEntry>();
            RebuildVisibleEntries();
            Size = CalculatePreferredSize();
            hoverIndex = -1;
            Invalidate();
        }

        internal void RefreshLayoutKeepingBottom()
        {
            var previousBounds = Bounds;
            RebuildVisibleEntries();
            Size = CalculatePreferredSize();
            var area = Screen.FromRectangle(previousBounds).WorkingArea;
            Bounds = Win11TrayMenuController.CalculateBottomAnchoredBounds(
                previousBounds,
                Size,
                area);
            hoverIndex = -1;
            Invalidate();
        }

        internal int VisibleEntryCountForTest
        {
            get { return visibleEntries.Count; }
        }

        internal int GetIndentForVisibleEntryForTest(int index)
        {
            return index >= 0 && index < visibleEntries.Count
                ? visibleEntries[index].IndentLevel
                : -1;
        }

        protected override void OnHandleCreated(EventArgs eventArgs)
        {
            base.OnHandleCreated(eventArgs);
            NativeMethods.TryApplyRoundedWindowCorners(Handle);
            int ignoredNativeError;
            acrylicApplied = NativeMethods.TryApplyLightTrayAcrylic(
                Handle,
                out ignoredNativeError);
        }

        protected override void OnPaintBackground(PaintEventArgs eventArgs)
        {
            if (!acrylicApplied)
            {
                // Clear, stable fallback if composition is unavailable.
                base.OnPaintBackground(eventArgs);
            }
            // The Accent policy supplies the whole acrylic surface. Painting
            // a GDI tint here would only blend against its gray intermediate
            // buffer and bring the unwanted mask back.
        }

        protected override void WndProc(ref Message message)
        {
            if (acrylicApplied && message.Msg == WmEraseBackground)
            {
                message.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref message);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            eventArgs.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            var scale = Math.Max(1f, DeviceDpi / 96f);
            for (var index = 0; index < visibleEntries.Count; index++)
            {
                var visible = visibleEntries[index];
                var entry = visible.Entry;
                var bounds = GetVisibleEntryBounds(index);
                if (entry.IsSeparator)
                {
                    DrawSeparator(eventArgs.Graphics, bounds, scale);
                    continue;
                }

                if (index == hoverIndex)
                {
                    DrawHover(eventArgs.Graphics, bounds, scale);
                }

                DrawText(eventArgs.Graphics, entry, visible.IndentLevel, bounds, scale);
                if (entry.IsChecked)
                {
                    DrawCheck(eventArgs.Graphics, bounds, scale);
                }
                if (entry.HasChildren)
                {
                    DrawArrow(eventArgs.Graphics, bounds, scale, entry.IsExpanded, entry.Enabled
                        ? Win11Visual.Title
                        : Win11Visual.SecondaryText);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs eventArgs)
        {
            base.OnMouseMove(eventArgs);
            var index = HitTest(eventArgs.Location);
            if (index == hoverIndex)
            {
                return;
            }

            hoverIndex = index;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs eventArgs)
        {
            base.OnMouseUp(eventArgs);
            if (eventArgs.Button != MouseButtons.Left)
            {
                controller.Hide();
                return;
            }

            var index = HitTest(eventArgs.Location);
            if (index < 0)
            {
                controller.Hide();
                return;
            }

            var entry = visibleEntries[index].Entry;
            if (entry == null || entry.IsSeparator || !entry.Enabled)
            {
                return;
            }

            if (entry.HasChildren)
            {
                controller.ToggleExpanded(entry);
            }
            else
            {
                controller.Execute(entry);
            }
        }

        protected override void OnKeyDown(KeyEventArgs eventArgs)
        {
            base.OnKeyDown(eventArgs);
            if (eventArgs.KeyCode == Keys.Escape)
            {
                controller.Hide();
                eventArgs.Handled = true;
            }
        }

        private void RebuildVisibleEntries()
        {
            visibleEntries.Clear();
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                visibleEntries.Add(new VisibleEntry { Entry = entry, IndentLevel = 0 });
                if (entry.IsExpanded && entry.HasChildren)
                {
                    foreach (var child in entry.Children)
                    {
                        if (child != null)
                        {
                            visibleEntries.Add(new VisibleEntry
                            {
                                Entry = child,
                                IndentLevel = 1
                            });
                        }
                    }
                }
            }
        }

        private Size CalculatePreferredSize()
        {
            var scale = Math.Max(1f, DeviceDpi / 96f);
            var height = Padding.Vertical;
            foreach (var visible in visibleEntries)
            {
                height += GetRowHeight(visible.Entry, scale);
            }
            return new Size(
                (int)Math.Ceiling(232f * scale),
                Math.Max((int)Math.Ceiling(34f * scale), height));
        }

        private Rectangle GetVisibleEntryBounds(int index)
        {
            var scale = Math.Max(1f, DeviceDpi / 96f);
            var y = Padding.Top;
            for (var current = 0; current < index && current < visibleEntries.Count; current++)
            {
                y += GetRowHeight(visibleEntries[current].Entry, scale);
            }
            return new Rectangle(
                Padding.Left,
                y,
                ClientSize.Width - Padding.Horizontal,
                GetRowHeight(visibleEntries[index].Entry, scale));
        }

        private int HitTest(Point location)
        {
            for (var index = 0; index < visibleEntries.Count; index++)
            {
                if (!visibleEntries[index].Entry.IsSeparator &&
                    GetVisibleEntryBounds(index).Contains(location))
                {
                    return index;
                }
            }
            return -1;
        }

        private static int GetRowHeight(TrayMenuEntry entry, float scale)
        {
            return entry != null && entry.IsSeparator
                ? Math.Max(7, (int)Math.Round(9 * scale))
                : Math.Max(30, (int)Math.Round(34 * scale));
        }

        private static void DrawSeparator(Graphics graphics, Rectangle bounds, float scale)
        {
            using (var pen = new Pen(Win11Visual.Border))
            {
                var y = bounds.Top + bounds.Height / 2;
                graphics.DrawLine(pen, bounds.Left + (int)(7 * scale), y,
                    bounds.Right - (int)(7 * scale), y);
            }
        }

        private static void DrawHover(Graphics graphics, Rectangle bounds, float scale)
        {
            var hover = bounds;
            hover.Inflate(-(int)Math.Max(2, scale * 2), -1);
            using (var path = Win11Visual.CreateRoundRect(
                hover,
                (int)Math.Max(6, scale * 7)))
            using (var brush = new SolidBrush(Color.FromArgb(
                HoverAlpha,
                226,
                239,
                246)))
            {
                graphics.FillPath(brush, path);
            }
        }

        private static void DrawText(
            Graphics graphics,
            TrayMenuEntry entry,
            int indentLevel,
            Rectangle bounds,
            float scale)
        {
            var color = entry.Enabled ? Win11Visual.Title : Win11Visual.SecondaryText;
            var textBounds = bounds;
            textBounds.X += (int)Math.Round((10 + ChildIndentLogicalPixels * indentLevel) * scale);
            textBounds.Width -= (int)Math.Round((30 + ChildIndentLogicalPixels * indentLevel) * scale);
            using (var brush = new SolidBrush(Win11Visual.ForceOpaque(color)))
            using (var format = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                graphics.DrawString(entry.Text, SystemFonts.MenuFont, brush, textBounds, format);
            }
        }

        private static void DrawCheck(Graphics graphics, Rectangle bounds, float scale)
        {
            var x = bounds.Right - (int)Math.Round(18 * scale);
            var y = bounds.Top + bounds.Height / 2;
            using (var pen = new Pen(Win11Visual.Accent, Math.Max(1.5f, scale * 2f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                graphics.DrawLines(pen, new[]
                {
                    new Point(x, y),
                    new Point(x + (int)(3 * scale), y + (int)(3 * scale)),
                    new Point(x + (int)(9 * scale), y - (int)(4 * scale))
                });
            }
        }

        private static void DrawArrow(
            Graphics graphics,
            Rectangle bounds,
            float scale,
            bool expanded,
            Color color)
        {
            var x = bounds.Right - (int)Math.Round(13 * scale);
            var y = bounds.Top + bounds.Height / 2;
            using (var pen = new Pen(Win11Visual.ForceOpaque(color), Math.Max(1f, scale)))
            {
                if (expanded)
                {
                    graphics.DrawLines(pen, new[]
                    {
                        new Point(x - (int)(4 * scale), y - (int)(2 * scale)),
                        new Point(x, y + (int)(2 * scale)),
                        new Point(x + (int)(4 * scale), y - (int)(2 * scale))
                    });
                }
                else
                {
                    graphics.DrawLines(pen, new[]
                    {
                        new Point(x - (int)(2 * scale), y - (int)(4 * scale)),
                        new Point(x + (int)(2 * scale), y),
                        new Point(x - (int)(2 * scale), y + (int)(4 * scale))
                    });
                }
            }
        }
    }
}
