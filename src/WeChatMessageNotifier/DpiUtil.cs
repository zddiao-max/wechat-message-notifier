using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WeChatMessageNotifier
{
    internal static class DpiUtil
    {
        internal const int BaselineDpi = 96;

        internal static readonly DpiMetrics DefaultMetrics =
            DpiMetrics.FromScale(1.0f);

        internal static float ScaleFromDpi(int dpi)
        {
            if (dpi <= 0)
            {
                return 1.0f;
            }
            return dpi / (float)BaselineDpi;
        }

        internal static int ScaleInt(int value, float scale)
        {
            return (int)Math.Round(value * scale);
        }

        internal static float ScaleFloat(float value, float scale)
        {
            return value * scale;
        }

        internal static float GetScaleForWindow(IntPtr hWnd)
        {
            if (hWnd != IntPtr.Zero)
            {
                try
                {
                    var dpi = GetDpiForWindow(hWnd);
                    if (dpi > 0)
                    {
                        return ScaleFromDpi((int)dpi);
                    }
                }
                catch (EntryPointNotFoundException)
                {
                }
                catch (DllNotFoundException)
                {
                }
            }

            using (var graphics = hWnd == IntPtr.Zero
                ? Graphics.FromHwnd(IntPtr.Zero)
                : Graphics.FromHwnd(hWnd))
            {
                return Math.Max(1.0f, graphics.DpiX / BaselineDpi);
            }
        }

        internal static float GetScaleForScreen(Screen screen)
        {
            if (screen == null)
            {
                return 1.0f;
            }

            try
            {
                var point = new NativePoint
                {
                    X = screen.Bounds.Left + Math.Max(1, screen.Bounds.Width / 2),
                    Y = screen.Bounds.Top + Math.Max(1, screen.Bounds.Height / 2)
                };
                var monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
                if (monitor != IntPtr.Zero)
                {
                    uint dpiX;
                    uint dpiY;
                    if (GetDpiForMonitor(
                            monitor,
                            MonitorDpiType.Effective,
                            out dpiX,
                            out dpiY) == 0 &&
                        dpiX > 0)
                    {
                        return ScaleFromDpi((int)dpiX);
                    }
                }
            }
            catch (EntryPointNotFoundException)
            {
            }
            catch (DllNotFoundException)
            {
            }

            return GetScaleForWindow(IntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(
            NativePoint point,
            uint flags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(
            IntPtr monitor,
            MonitorDpiType dpiType,
            out uint dpiX,
            out uint dpiY);

        private const uint MonitorDefaultToNearest = 2;

        private enum MonitorDpiType
        {
            Effective = 0
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            internal int X;
            internal int Y;
        }
    }
}
