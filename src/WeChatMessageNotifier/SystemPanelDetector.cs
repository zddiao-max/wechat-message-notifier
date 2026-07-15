using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Automation;
using System.Windows.Forms;

namespace WeChatMessageNotifier
{
    internal sealed class SystemPanelSnapshot
    {
        internal Rectangle QuickSettingsBounds { get; set; }
        internal Rectangle NotificationCenterBounds { get; set; }
        internal bool QuickSettingsVisible { get; set; }
        internal bool NotificationCenterVisible { get; set; }
        internal IList<string> DiagnosticLines { get; private set; }
        // Used only by the short-lived diagnostic capture.  It deliberately
        // contains window structure and bounds, never titles or UI content.
        internal string DiagnosticSignature { get; set; }

        internal SystemPanelSnapshot()
        {
            DiagnosticLines = new List<string>();
        }
    }

    // Windows 11 hosts these panels in version-dependent XAML windows. We
    // intentionally use a structural score (process, visibility, cloak state,
    // screen anchoring, and live bounds) instead of hardcoding one class name.
    internal sealed class SystemPanelDetector
    {
        private DateTime nextUiaDiagnosticAt = DateTime.MinValue;

        internal SystemPanelSnapshot Detect(Rectangle workingArea, bool includeDiagnostics)
        {
            var snapshot = new SystemPanelSnapshot();
            var diagnosticSignature = includeDiagnostics ? new StringBuilder() : null;
            NativeMethods.EnumWindows(delegate(IntPtr hWnd, IntPtr ignored)
            {
                Rectangle bounds;
                var visible = NativeMethods.IsWindowVisible(hWnd);
                var cloaked = NativeMethods.IsWindowCloaked(hWnd);
                if (!NativeMethods.TryGetWindowBounds(hWnd, out bounds))
                {
                    return true;
                }

                uint processId;
                NativeMethods.GetWindowThreadProcessId(hWnd, out processId);
                var processName = ReadProcessName(processId);
                var className = NativeMethods.ReadClassName(hWnd);
                var screen = Screen.FromRectangle(bounds).Bounds;
                var rightAnchored = Math.Abs(screen.Right - bounds.Right) <= 16;
                var bottomAnchored = Math.Abs(screen.Bottom - bounds.Bottom) <= 80 ||
                                     Math.Abs(workingArea.Bottom - bounds.Bottom) <= 80;
                var shellProcess = IsShellProcess(processName);

                // Timed diagnostics must not enumerate-and-log every desktop
                // window on the UI thread.  It is intentionally limited to
                // likely Windows flyout hosts and contains no titles/content.
                // The broad process list accommodates Windows 11 builds that
                // host flyouts in XAML, ApplicationFrameHost or WebView2.
                if (includeDiagnostics && IsDiagnosticCandidate(
                    processName, className, bounds, screen))
                {
                    snapshot.DiagnosticLines.Add(
                        "SystemPanelWindow HWND=" + hWnd.ToInt64().ToString("X") +
                        " Process=" + processName +
                        " Class=" + className +
                        " Bounds=" + bounds.Left + "," + bounds.Top + "," +
                        bounds.Width + "x" + bounds.Height +
                        " Visible=" + visible + " Cloaked=" + cloaked +
                        " Screen=" + Screen.FromRectangle(bounds).DeviceName);

                    // The Windows 11 flyout host is version-dependent.  Keep
                    // a compact, privacy-safe fingerprint so the timed
                    // capture writes another sample when an as-yet unknown
                    // host appears or changes bounds; the old signature only
                    // changed after this detector had already classified it.
                    diagnosticSignature.Append(hWnd.ToInt64().ToString("X"))
                        .Append('|').Append(processName)
                        .Append('|').Append(className)
                        .Append('|').Append(bounds.Left).Append(',')
                        .Append(bounds.Top).Append(',')
                        .Append(bounds.Width).Append('x').Append(bounds.Height)
                        .Append('|').Append(visible).Append('|').Append(cloaked)
                        .Append(';');

                    if (IsPotentialPanelHost(processName, className, bounds, screen))
                    {
                        RecordDiagnosticChildWindows(
                            hWnd,
                            snapshot,
                            diagnosticSignature);
                    }
                }

                if (!visible || cloaked)
                {
                    return true;
                }

                if (!shellProcess || bounds.Width < 180 || bounds.Height < 120)
                {
                    return true;
                }

                // Quick Settings grows upward from the taskbar and is much
                // shorter than the screen. Keep the largest live candidate.
                if (rightAnchored && bottomAnchored &&
                    bounds.Height < screen.Height * 0.72 &&
                    bounds.Width < screen.Width * 0.78)
                {
                    if (!snapshot.QuickSettingsVisible ||
                        bounds.Height * bounds.Width >
                        snapshot.QuickSettingsBounds.Height * snapshot.QuickSettingsBounds.Width)
                    {
                        snapshot.QuickSettingsBounds = bounds;
                        snapshot.QuickSettingsVisible = true;
                    }
                }

                // Notification Center/calendar is a tall panel anchored to
                // the right edge. It must not be constrained by toast sizing.
                if (rightAnchored && bounds.Height >= screen.Height * 0.35 &&
                    bounds.Width <= screen.Width * 0.60)
                {
                    if (!snapshot.NotificationCenterVisible ||
                        bounds.Height > snapshot.NotificationCenterBounds.Height)
                    {
                        snapshot.NotificationCenterBounds = bounds;
                        snapshot.NotificationCenterVisible = true;
                    }
                }
                return true;
            }, IntPtr.Zero);
            if (includeDiagnostics && DateTime.UtcNow >= nextUiaDiagnosticAt)
            {
                // XAML flyouts can have no individually addressable HWND.
                // Probe the desktop UIA root at most once per second, never
                // read element Name/help text, and log only structural data.
                nextUiaDiagnosticAt = DateTime.UtcNow.AddSeconds(1);
                RecordUiaDiagnosticCandidates(snapshot, diagnosticSignature);
            }
            if (diagnosticSignature != null)
            {
                snapshot.DiagnosticSignature = diagnosticSignature.ToString();
            }
            return snapshot;
        }

        private static bool IsShellProcess(string processName)
        {
            return string.Equals(processName, "explorer", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(processName, "ShellExperienceHost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(processName, "StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDiagnosticCandidate(
            string processName,
            string className,
            Rectangle bounds,
            Rectangle screen)
        {
            if (bounds.Width < 80 || bounds.Height < 40 ||
                !bounds.IntersectsWith(screen))
            {
                return false;
            }

            return IsShellProcess(processName) ||
                   string.Equals(processName, "ShellHost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(processName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(processName, "RuntimeBroker", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(processName, "TextInputHost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(processName, "msedgewebview2", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(processName, "Widgets", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(processName, "SystemSettings", StringComparison.OrdinalIgnoreCase) ||
                   className.IndexOf("Xaml", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsPotentialPanelHost(
            string processName,
            string className,
            Rectangle bounds,
            Rectangle screen)
        {
            if (className.IndexOf("XamlExplorerHostIslandWindow",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                string.Equals(processName, "ApplicationFrameHost",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(processName, "ShellHost",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Current Windows 11 builds can place the flyout composition
            // beneath a full-screen WebView2 host. Limit this to that one
            // shape instead of walking arbitrary application child windows.
            return string.Equals(processName, "msedgewebview2",
                       StringComparison.OrdinalIgnoreCase) &&
                   bounds.Width >= screen.Width * 0.8 &&
                   bounds.Height >= screen.Height * 0.8;
        }

        private static void RecordDiagnosticChildWindows(
            IntPtr parent,
            SystemPanelSnapshot snapshot,
            StringBuilder signature)
        {
            NativeMethods.EnumChildWindows(parent, delegate(IntPtr child, IntPtr ignored)
            {
                Rectangle bounds;
                if (!NativeMethods.TryGetWindowBounds(child, out bounds) ||
                    bounds.Width < 80 || bounds.Height < 40)
                {
                    return true;
                }

                var screen = Screen.FromRectangle(bounds);
                if (!bounds.IntersectsWith(screen.Bounds))
                {
                    return true;
                }

                uint processId;
                NativeMethods.GetWindowThreadProcessId(child, out processId);
                var processName = ReadProcessName(processId);
                var className = NativeMethods.ReadClassName(child);
                var visible = NativeMethods.IsWindowVisible(child);
                var cloaked = NativeMethods.IsWindowCloaked(child);
                snapshot.DiagnosticLines.Add(
                    "SystemPanelChild HWND=" + child.ToInt64().ToString("X") +
                    " Parent=" + parent.ToInt64().ToString("X") +
                    " Process=" + processName +
                    " Class=" + className +
                    " Bounds=" + bounds.Left + "," + bounds.Top + "," +
                    bounds.Width + "x" + bounds.Height +
                    " Visible=" + visible + " Cloaked=" + cloaked +
                    " Screen=" + screen.DeviceName);
                signature.Append("child:").Append(child.ToInt64().ToString("X"))
                    .Append('|').Append(processName).Append('|').Append(className)
                    .Append('|').Append(bounds.Left).Append(',').Append(bounds.Top)
                    .Append(',').Append(bounds.Width).Append('x').Append(bounds.Height)
                    .Append('|').Append(visible).Append('|').Append(cloaked).Append(';');
                return true;
            }, IntPtr.Zero);
        }

        private static void RecordUiaDiagnosticCandidates(
            SystemPanelSnapshot snapshot,
            StringBuilder signature)
        {
            try
            {
                var elements = AutomationElement.RootElement.FindAll(
                    TreeScope.Children,
                    Condition.TrueCondition);
                foreach (AutomationElement element in elements)
                {
                    try
                    {
                        var current = element.Current;
                        var rect = current.BoundingRectangle;
                        if (rect.IsEmpty || rect.Width < 120 || rect.Height < 80)
                        {
                            continue;
                        }

                        var bounds = Rectangle.FromLTRB(
                            (int)Math.Floor(rect.Left),
                            (int)Math.Floor(rect.Top),
                            (int)Math.Ceiling(rect.Right),
                            (int)Math.Ceiling(rect.Bottom));
                        var screen = Screen.FromRectangle(bounds);
                        var processName = ReadProcessName((uint)current.ProcessId);
                        if (!IsDiagnosticCandidate(
                            processName,
                            current.ClassName ?? string.Empty,
                            bounds,
                            screen.Bounds))
                        {
                            continue;
                        }

                        snapshot.DiagnosticLines.Add(
                            "SystemPanelUia Process=" + processName +
                            " Class=" + (current.ClassName ?? string.Empty) +
                            " AutomationId=" + (current.AutomationId ?? string.Empty) +
                            " ControlType=" + current.ControlType.ProgrammaticName +
                            " Bounds=" + bounds.Left + "," + bounds.Top + "," +
                            bounds.Width + "x" + bounds.Height +
                            " Offscreen=" + current.IsOffscreen +
                            " Screen=" + screen.DeviceName);
                        signature.Append("uia:").Append(processName).Append('|')
                            .Append(current.ClassName ?? string.Empty).Append('|')
                            .Append(current.AutomationId ?? string.Empty).Append('|')
                            .Append(bounds.Left).Append(',').Append(bounds.Top)
                            .Append(',').Append(bounds.Width).Append('x').Append(bounds.Height)
                            .Append('|').Append(current.IsOffscreen).Append(';');
                    }
                    catch (ElementNotAvailableException)
                    {
                        // The shell may close the flyout while it is queried.
                    }
                }
            }
            catch (Exception)
            {
                // UIA is diagnostic-only; failure must never affect alerts.
            }
        }

        private static string ReadProcessName(uint processId)
        {
            try
            {
                using (var process = Process.GetProcessById((int)processId))
                {
                    return process.ProcessName;
                }
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
