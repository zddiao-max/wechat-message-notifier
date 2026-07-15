using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace WeChatMessageNotifier
{
    internal sealed class PopupManager
    {
        internal const int AnimationIntervalMilliseconds =
            MotionSettings.FrameIntervalMilliseconds;
        internal const int ObstructionIntervalMilliseconds =
            MotionSettings.ObstructionIntervalMilliseconds;

        private readonly Dictionary<string, PopupEntry> entries =
            new Dictionary<string, PopupEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, PopupCardControl> cards =
            new Dictionary<string, PopupCardControl>(StringComparer.Ordinal);
        private readonly Dictionary<string, PopupAnimationState> states =
            new Dictionary<string, PopupAnimationState>(StringComparer.Ordinal);
        private readonly List<string> order = new List<string>();
        private readonly List<string> completedExits = new List<string>();
        private readonly Action<string> activateWeChat;
        private readonly AnimationClock animationClock;
        private readonly Timer obstructionTimer;
        private readonly int currentProcessId;
        private readonly bool userInterfaceEnabled;
        private readonly AvoidanceMotionState avoidanceMotion =
            new AvoidanceMotionState();
        private readonly PanelAvoidanceState panelAvoidance =
            new PanelAvoidanceState();
        private readonly SystemPanelDetector systemPanelDetector =
            new SystemPanelDetector();
        private PopupHostForm host;
        private Rectangle workingArea = Rectangle.Empty;
        private MotionMode motionMode;
        private PopupVisualMode visualMode;
        private DpiMetrics metrics = DpiUtil.DefaultMetrics;
        private int headlessHostHeight = DpiUtil.DefaultMetrics.CardHeight;

        internal PopupManager(Action<string> activateWeChat)
            : this(activateWeChat, true, MotionMode.Standard)
        {
        }

        internal PopupManager(
            Action<string> activateWeChat,
            bool userInterfaceEnabled)
            : this(
                activateWeChat,
                userInterfaceEnabled,
                MotionMode.Standard)
        {
        }

        internal PopupManager(
            Action<string> activateWeChat,
            bool userInterfaceEnabled,
            MotionMode motionMode)
            : this(
                activateWeChat,
                userInterfaceEnabled,
                motionMode,
                PopupVisualMode.Glass)
        {
        }

        internal PopupManager(
            Action<string> activateWeChat,
            bool userInterfaceEnabled,
            MotionMode motionMode,
            PopupVisualMode visualMode)
        {
            this.activateWeChat = activateWeChat;
            this.userInterfaceEnabled = userInterfaceEnabled;
            this.motionMode = motionMode;
            this.visualMode = visualMode;
            currentProcessId = Process.GetCurrentProcess().Id;
            animationClock = new AnimationClock(
                delegate { AdvanceAnimationFrame(); });
            obstructionTimer = new Timer
            {
                Interval = ObstructionIntervalMilliseconds
            };
            obstructionTimer.Tick += delegate { DetectObstruction(); };
        }

        internal int ActiveCount
        {
            get { return entries.Count; }
        }

        internal bool IsAnimationTimerRunning
        {
            get { return animationClock.IsRunning; }
        }

        internal bool IsObstructionTimerRunning
        {
            get { return obstructionTimer.Enabled; }
        }

        internal MotionMode MotionMode
        {
            get { return motionMode; }
        }

        internal PopupVisualMode VisualMode
        {
            get { return visualMode; }
        }

        internal int HostHeightForTest
        {
            get { return host == null ? headlessHostHeight : host.Height; }
        }

        internal int HostBottomForTest
        {
            get { return host == null ? 0 : host.Bottom; }
        }

        internal double CurrentOffsetXForTest { get { return panelAvoidance.CurrentOffsetX; } }
        internal double CurrentOffsetYForTest { get { return panelAvoidance.CurrentOffsetY; } }
        internal double TargetOffsetXForTest { get { return panelAvoidance.TargetOffsetX; } }
        internal double TargetOffsetYForTest { get { return panelAvoidance.TargetOffsetY; } }

        internal bool IsHostVisibleForTest
        {
            get { return host != null && host.Visible; }
        }

        internal string LastVisualDiagnostics
        {
            get
            {
                return host != null && !host.IsDisposed
                    ? host.VisualDiagnostics
                    : "VisualMode=" + visualMode +
                      ", DwmBackdropApplied=False" +
                      ", AccentApplied=False" +
                      ", ExtendFrameApplied=False" +
                      ", EffectiveVisualMode=None" +
                      ", BackdropKind=None";
            }
        }

        internal PopupAnimationPhase GetAnimationPhaseForTest(
            string sessionKey)
        {
            return states[sessionKey].Phase;
        }

        internal double GetCurrentYForTest(string sessionKey)
        {
            return states[sessionKey].LayoutY.Current;
        }

        internal double GetTargetYForTest(string sessionKey)
        {
            return states[sessionKey].LayoutY.Target;
        }

        internal double GetVisualYForTest(string sessionKey)
        {
            var state = states[sessionKey];
            return state.LayoutY.Current + state.YOffset.Current;
        }

        internal double GetOpacityForTest(string sessionKey)
        {
            return states[sessionKey].Opacity.Current;
        }

        internal double GetScaleForTest(string sessionKey)
        {
            return states[sessionKey].Scale.Current;
        }

        internal void AdvanceAnimationFrameForTest()
        {
            AdvanceAnimationFrame();
        }

        internal void SetPanelTargetsForTest(double x, double y)
        {
            panelAvoidance.SetTargets(x, y, motionMode);
        }

        internal void LogSystemPanelStructure(Logger logger)
        {
            var snapshot = GetSystemPanelSnapshotForDiagnostic(true);
            foreach (var line in snapshot.DiagnosticLines)
            {
                logger.Info(line);
            }
            logger.Info("SystemPanelSnapshot QuickSettingsVisible=" + snapshot.QuickSettingsVisible +
                " QuickSettingsBounds=" + FormatBounds(snapshot.QuickSettingsBounds) +
                " NotificationCenterVisible=" + snapshot.NotificationCenterVisible +
                " NotificationCenterBounds=" + FormatBounds(snapshot.NotificationCenterBounds));
        }

        internal SystemPanelSnapshot GetSystemPanelSnapshotForDiagnostic(
            bool includeDiagnostics)
        {
            var area = workingArea == Rectangle.Empty
                ? Screen.PrimaryScreen.WorkingArea
                : workingArea;
            return systemPanelDetector.Detect(area, includeDiagnostics);
        }

        internal void Show(string contact, string preview)
        {
            Show(contact, contact, preview, false);
        }

        internal PopupEntry Show(
            string sessionKey,
            string contact,
            string preview,
            bool privacyMode)
        {
            if (string.IsNullOrWhiteSpace(sessionKey))
            {
                sessionKey = contact;
            }

            PopupEntry entry;
            var isNew = !entries.TryGetValue(sessionKey, out entry);
            if (isNew)
            {
                entry = new PopupEntry(
                    sessionKey,
                    contact,
                    preview,
                    privacyMode);
                entries.Add(sessionKey, entry);
            }
            else
            {
                entry.Update(contact, preview, privacyMode);
            }

            if (userInterfaceEnabled)
            {
                EnsureHost();
                EnsureWorkingArea();
                PopupCardControl card;
                PopupAnimationState state;
                if (!cards.TryGetValue(sessionKey, out card))
                {
                    card = CreateCard(entry);
                    cards.Add(sessionKey, card);
                    order.Add(sessionKey);
                    var requiredHeight =
                        PopupHostForm.CalculateRequiredHeight(
                            order.Count,
                            metrics);
                    var growth = Math.Max(
                        0,
                        requiredHeight - host.Height);
                    if (growth > 0)
                    {
                        foreach (var existing in states.Values)
                        {
                            existing.ShiftLayoutCurrent(growth);
                        }
                        ApplyAllCardVisuals();
                        host.GrowToFit(order.Count);
                    }

                    var targetY = CalculateTargetY(
                        host.Height,
                        order.Count,
                        order.Count - 1,
                        metrics);
                    state = new PopupAnimationState(
                        targetY,
                        motionMode,
                        metrics.Scale);
                    states.Add(sessionKey, state);
                    // Apply the entering offset/opacity/scale before parenting
                    // the card. A visible host must never paint a newly added
                    // card once at its final stable appearance.
                    ApplyCardVisual(sessionKey);
                    host.AddCard(card);
                }
                else
                {
                    state = states[sessionKey];
                    if (!order.Contains(sessionKey))
                    {
                        order.Add(sessionKey);
                    }
                    card.UpdateEntry(entry, true);
                    state.BeginUpdate(motionMode);
                }

                RecalculateLayoutTargets();
                ApplyAllCardVisuals();
                host.RefreshCardGeometry();
                if (!host.Visible)
                {
                    host.Show();
                    RefreshCardVisualMode();
                }
                StartAnimationIfNeeded();
            }
            else
            {
                PrepareHeadlessAnimation(sessionKey, isNew);
            }

            if (!obstructionTimer.Enabled)
            {
                obstructionTimer.Start();
            }
            if (userInterfaceEnabled)
            {
                DetectObstruction();
            }
            return entry;
        }

        internal PopupEntry GetEntry(string sessionKey)
        {
            PopupEntry entry;
            return entries.TryGetValue(sessionKey, out entry)
                ? entry
                : null;
        }

        internal void SetPrivacyMode(bool privacyMode)
        {
            foreach (var item in entries)
            {
                item.Value.SetPrivacyMode(privacyMode);
                PopupCardControl card;
                if (cards.TryGetValue(item.Key, out card))
                {
                    card.UpdateEntry(item.Value, false);
                }
            }
        }

        internal void SetMotionMode(MotionMode mode)
        {
            motionMode = mode;
            if (!userInterfaceEnabled || mode != MotionMode.Off)
            {
                return;
            }

            completedExits.Clear();
            foreach (var item in states)
            {
                if (item.Value.Phase == PopupAnimationPhase.Exiting)
                {
                    completedExits.Add(item.Key);
                }
                else
                {
                    item.Value.SnapStable();
                    ApplyCardVisual(item.Key);
                }
            }
            foreach (var key in completedExits)
            {
                FinalizeExit(key);
            }
            avoidanceMotion.SnapToTarget();
            panelAvoidance.SnapToTarget();
            PositionHost();
            PositionHost();
            animationClock.Stop();
        }

        internal void SetVisualMode(PopupVisualMode mode)
        {
            visualMode = mode;
            var effective = GetRequestedCardVisualMode();
            if (host != null && !host.IsDisposed)
            {
                host.UpdateVisualMode(mode);
                effective = host.EffectiveVisualMode;
            }
            foreach (var card in cards.Values)
            {
                card.UpdateVisualMode(effective);
            }
        }

        internal void Close(string sessionKey)
        {
            if (!entries.ContainsKey(sessionKey))
            {
                return;
            }

            if (!userInterfaceEnabled || motionMode == MotionMode.Off)
            {
                FinalizeExit(sessionKey);
                return;
            }

            PopupAnimationState state;
            if (!states.TryGetValue(sessionKey, out state) ||
                state.Phase == PopupAnimationPhase.Exiting)
            {
                return;
            }

            PopupCardControl card;
            if (cards.TryGetValue(sessionKey, out card))
            {
                card.StopLifetime();
            }
            order.Remove(sessionKey);
            state.BeginExit(motionMode);
            RecalculateLayoutTargets();
            animationClock.Start();
        }

        internal void CloseAll()
        {
            animationClock.Stop();
            obstructionTimer.Stop();
            foreach (var card in cards.Values)
            {
                card.StopLifetime();
                card.Dispose();
            }
            cards.Clear();
            states.Clear();
            entries.Clear();
            order.Clear();
            headlessHostHeight = metrics.CardHeight;

            if (host != null)
            {
                host.Close();
                host.Dispose();
                host = null;
            }

            animationClock.Dispose();
            obstructionTimer.Dispose();
        }

        internal static int CalculateTargetY(
            int hostHeight,
            int activeCount,
            int index)
        {
            if (activeCount <= 0)
            {
                return 0;
            }
            var stackHeight =
                activeCount * DpiUtil.DefaultMetrics.CardHeight +
                (activeCount - 1) * DpiUtil.DefaultMetrics.CardSpacing;
            return hostHeight -
                   stackHeight +
                   index *
                   (DpiUtil.DefaultMetrics.CardHeight +
                    DpiUtil.DefaultMetrics.CardSpacing);
        }

        internal static int CalculateTargetY(
            int hostHeight,
            int activeCount,
            int index,
            DpiMetrics metrics)
        {
            if (activeCount <= 0)
            {
                return 0;
            }
            var stackHeight =
                activeCount * metrics.CardHeight +
                (activeCount - 1) * metrics.CardSpacing;
            return hostHeight -
                   stackHeight +
                   index *
                   (metrics.CardHeight + metrics.CardSpacing);
        }

        internal static bool ShouldRunAnimation(
            IEnumerable<PopupAnimationState> animationStates,
            AvoidanceMotionState hostAvoidance)
        {
            if (hostAvoidance != null && !hostAvoidance.IsSettled)
            {
                return true;
            }
            if (animationStates == null)
            {
                return false;
            }
            foreach (var state in animationStates)
            {
                if (state != null && state.NeedsAnimation)
                {
                    return true;
                }
            }
            return false;
        }

        private PopupCardControl CreateCard(PopupEntry entry)
        {
            var card = new PopupCardControl(
                entry,
                metrics.Scale,
                GetRequestedCardVisualMode());
            card.CardClicked += delegate
            {
                if (activateWeChat != null)
                {
                    activateWeChat(entry.SessionKey);
                }
                Close(entry.SessionKey);
            };
            card.CloseRequested += delegate { Close(entry.SessionKey); };
            return card;
        }

        private void EnsureHost()
        {
            if (host == null || host.IsDisposed)
            {
                host = new PopupHostForm(metrics.Scale, visualMode);
                host.DpiScaleChanged += delegate
                {
                    ApplyDpiScale(host.DpiScale);
                    RecalculateLayoutTargets();
                    ApplyAllCardVisuals();
                    PositionHost();
                };
                RefreshCardVisualMode();
            }
        }

        private void EnsureWorkingArea()
        {
            var foreground = NativeMethods.GetForegroundWindow();
            var targetScreen = foreground != IntPtr.Zero
                ? Screen.FromHandle(foreground)
                : Screen.PrimaryScreen;
            var newScale = DpiUtil.GetScaleForScreen(targetScreen);
            var areaChanged = workingArea != targetScreen.WorkingArea;
            var scaleChanged = Math.Abs(metrics.Scale - newScale) >= 0.01f;
            if (!areaChanged && !scaleChanged && workingArea != Rectangle.Empty)
            {
                return;
            }

            workingArea = targetScreen.WorkingArea;
            if (scaleChanged)
            {
                ApplyDpiScale(newScale);
                RecalculateLayoutTargets();
                ApplyAllCardVisuals();
            }
            PositionHost();
        }

        private void RecalculateLayoutTargets()
        {
            var layoutHeight = userInterfaceEnabled
                ? host.Height
                : headlessHostHeight;
            for (var index = 0; index < order.Count; index++)
            {
                PopupAnimationState state;
                if (states.TryGetValue(order[index], out state))
                {
                    state.SetLayoutTarget(
                        CalculateTargetY(
                            layoutHeight,
                            order.Count,
                            index,
                            metrics),
                        motionMode);
                }
            }
            if (motionMode == MotionMode.Off)
            {
                foreach (var key in order)
                {
                    ApplyCardVisual(key);
                }
            }
            else
            {
                StartAnimationIfNeeded();
            }
        }

        private void DetectObstruction()
        {
            if (entries.Count == 0 || !userInterfaceEnabled)
            {
                return;
            }

            EnsureWorkingArea();
            var targetChanged = avoidanceMotion.ReportDetection(
                CalculateAvoidanceOffset(workingArea),
                motionMode);
            var panels = systemPanelDetector.Detect(workingArea, false);
            var panelTargets = CalculatePanelAvoidanceOffsets(panels);
            var panelTargetChanged = panelAvoidance.SetTargets(
                panelTargets.X,
                panelTargets.Y,
                motionMode);
            obstructionTimer.Interval =
                panels.QuickSettingsVisible || panels.NotificationCenterVisible ||
                !panelAvoidance.IsSettled
                    ? 80
                    : ObstructionIntervalMilliseconds;
            if (!targetChanged && !panelTargetChanged)
            {
                return;
            }
            if (motionMode == MotionMode.Off)
            {
                PositionHost();
            }
            else
            {
                animationClock.Start();
            }
        }

        private void AdvanceAnimationFrame()
        {
            var anyAnimating = false;
            if (!panelAvoidance.IsSettled)
            {
                panelAvoidance.Advance(motionMode);
                PositionHost();
                anyAnimating = !panelAvoidance.IsSettled;
            }
            if (!avoidanceMotion.IsSettled)
            {
                avoidanceMotion.Advance(motionMode);
                PositionHost();
                anyAnimating = !avoidanceMotion.IsSettled;
            }

            completedExits.Clear();
            foreach (var item in states)
            {
                item.Value.Advance(motionMode);
                ApplyCardVisual(item.Key);
                if (item.Value.Phase == PopupAnimationPhase.Exited)
                {
                    completedExits.Add(item.Key);
                }
                else if (item.Value.NeedsAnimation)
                {
                    anyAnimating = true;
                }
            }

            foreach (var key in completedExits)
            {
                FinalizeExit(key);
            }

            if (!anyAnimating &&
                completedExits.Count == 0 &&
                !ShouldRunAnimation(states.Values, avoidanceMotion) &&
                panelAvoidance.IsSettled)
            {
                animationClock.Stop();
            }
        }

        private void ApplyCardVisual(string sessionKey)
        {
            PopupCardControl card;
            PopupAnimationState state;
            if (!cards.TryGetValue(sessionKey, out card) ||
                !states.TryGetValue(sessionKey, out state))
            {
                return;
            }

            var location = new Point(
                (int)Math.Round(state.XOffset.Current),
                (int)Math.Round(
                    state.LayoutY.Current +
                    state.YOffset.Current));
            if (card.Location != location)
            {
                card.Location = location;
            }
            card.ApplyVisual(
                state.Opacity.Current,
                state.Scale.Current);
        }

        private void ApplyAllCardVisuals()
        {
            foreach (var key in states.Keys)
            {
                ApplyCardVisual(key);
            }
        }

        private void PrepareHeadlessAnimation(
            string sessionKey,
            bool isNew)
        {
            PopupAnimationState state;
            if (isNew)
            {
                order.Add(sessionKey);
                var requiredHeight =
                    PopupHostForm.CalculateRequiredHeight(
                        order.Count,
                        metrics);
                var growth = Math.Max(
                    0,
                    requiredHeight - headlessHostHeight);
                if (growth > 0)
                {
                    foreach (var existing in states.Values)
                    {
                        existing.ShiftLayoutCurrent(growth);
                    }
                }
                headlessHostHeight = requiredHeight;
                state = new PopupAnimationState(
                    CalculateTargetY(
                        headlessHostHeight,
                        order.Count,
                        order.Count - 1,
                        metrics),
                    motionMode,
                    metrics.Scale);
                states.Add(sessionKey, state);
            }
            else if (states.TryGetValue(sessionKey, out state))
            {
                state.BeginUpdate(motionMode);
            }

            RecalculateLayoutTargets();
            StartAnimationIfNeeded();
        }

        private void FinalizeExit(string sessionKey)
        {
            entries.Remove(sessionKey);
            order.Remove(sessionKey);
            states.Remove(sessionKey);
            PopupCardControl card;
            if (cards.TryGetValue(sessionKey, out card))
            {
                cards.Remove(sessionKey);
                card.StopLifetime();
                if (host != null)
                {
                    host.RemoveCard(card);
                }
                card.Dispose();
            }

            if (entries.Count == 0)
            {
                StopActiveTimers();
                if (host != null)
                {
                    host.Hide();
                    host.ResetStackHeight();
                }
                headlessHostHeight = metrics.CardHeight;
                return;
            }
            CompactHostToActiveCards();
            RecalculateLayoutTargets();
            ApplyAllCardVisuals();
            if (host != null && !host.IsDisposed)
            {
                host.RefreshCardGeometry();
            }
            PositionHost();
        }

        private void CompactHostToActiveCards()
        {
            if (!userInterfaceEnabled || host == null || host.IsDisposed)
            {
                headlessHostHeight = PopupHostForm.CalculateRequiredHeight(
                    entries.Count,
                    metrics);
                return;
            }

            var delta = host.ShrinkToFit(entries.Count);
            if (delta <= 0)
            {
                return;
            }

            foreach (var state in states.Values)
            {
                state.ShiftLayoutCurrent(-delta);
            }
        }

        private void StartAnimationIfNeeded()
        {
            if (motionMode == MotionMode.Off)
            {
                return;
            }
            if (ShouldRunAnimation(states.Values, avoidanceMotion))
            {
                animationClock.Start();
            }
        }

        private void PositionHost()
        {
            if (host == null || workingArea == Rectangle.Empty)
            {
                return;
            }

            var target = new Point(
                workingArea.Right - host.Width - metrics.ScreenMargin +
                (int)Math.Round(panelAvoidance.CurrentOffsetX),
                workingArea.Bottom -
                host.Height -
                metrics.ScreenMargin -
                (int)Math.Round(avoidanceMotion.Current) +
                (int)Math.Round(panelAvoidance.CurrentOffsetY));
            if (host.Location != target)
            {
                host.Location = target;
            }
        }

        private int CalculateAvoidanceOffset(Rectangle area)
        {
            var left = area.Right - metrics.CardWidth - metrics.ScreenMargin;
            var top = area.Bottom - metrics.CardHeight - metrics.ScreenMargin;
            var samplePoints = new[]
            {
                new Point(
                    left + DpiUtil.ScaleInt(24, metrics.Scale),
                    top + DpiUtil.ScaleInt(24, metrics.Scale)),
                new Point(
                    left + metrics.CardWidth / 2,
                    top + metrics.CardHeight / 2),
                new Point(
                    left + metrics.CardWidth -
                    DpiUtil.ScaleInt(24, metrics.Scale),
                    top + metrics.CardHeight -
                    DpiUtil.ScaleInt(24, metrics.Scale))
            };

            var offset = 0;
            foreach (var point in samplePoints)
            {
                Rectangle obstruction;
                if (NativeMethods.TryGetExternalTopmostWindowAt(
                    point,
                    currentProcessId,
                    out obstruction))
                {
                    offset = Math.Max(
                        offset,
                        area.Bottom - obstruction.Top +
                        metrics.AvoidancePadding);
                }
            }
            return Math.Max(0, Math.Min(offset, area.Height / 2));
        }

        private Point CalculatePanelAvoidanceOffsets(SystemPanelSnapshot panels)
        {
            if (host == null || panels == null)
            {
                return Point.Empty;
            }
            var defaultBounds = new Rectangle(
                workingArea.Right - host.Width - metrics.ScreenMargin,
                workingArea.Bottom - host.Height - metrics.ScreenMargin,
                host.Width,
                host.Height);
            var x = 0;
            var y = 0;
            if (panels.QuickSettingsVisible && !panels.QuickSettingsBounds.IsEmpty &&
                defaultBounds.Right > panels.QuickSettingsBounds.Left &&
                defaultBounds.Bottom > panels.QuickSettingsBounds.Top)
            {
                y = panels.QuickSettingsBounds.Top - metrics.AvoidancePadding - defaultBounds.Bottom;
            }
            if (panels.NotificationCenterVisible && !panels.NotificationCenterBounds.IsEmpty &&
                defaultBounds.Right > panels.NotificationCenterBounds.Left &&
                defaultBounds.Bottom > panels.NotificationCenterBounds.Top)
            {
                x = panels.NotificationCenterBounds.Left - metrics.AvoidancePadding - defaultBounds.Right;
            }
            return new Point(Math.Min(0, x), Math.Min(0, y));
        }

        private void StopActiveTimers()
        {
            animationClock.Stop();
            obstructionTimer.Stop();
            avoidanceMotion.Reset();
            panelAvoidance.Reset();
            headlessHostHeight = metrics.CardHeight;
            workingArea = Rectangle.Empty;
        }

        private void ApplyDpiScale(float dpiScale)
        {
            metrics = DpiMetrics.FromScale(dpiScale);
            if (host != null && !host.IsDisposed)
            {
                host.UpdateDpiScale(dpiScale);
            }
            foreach (var card in cards.Values)
            {
                card.UpdateDpiScale(dpiScale);
            }
            foreach (var state in states.Values)
            {
                state.UpdateDpiScale(dpiScale);
            }
            if (!userInterfaceEnabled)
            {
                headlessHostHeight =
                    PopupHostForm.CalculateRequiredHeight(
                        order.Count,
                        metrics);
            }
        }

        private PopupVisualMode GetRequestedCardVisualMode()
        {
            if (visualMode == PopupVisualMode.Solid)
            {
                return PopupVisualMode.Solid;
            }
            if (host != null && !host.IsDisposed)
            {
                return host.EffectiveVisualMode;
            }
            return PopupVisualMode.Glass;
        }

        private static string FormatBounds(Rectangle bounds)
        {
            return bounds.IsEmpty
                ? "Empty"
                : bounds.Left + "," + bounds.Top + "," + bounds.Width + "x" + bounds.Height;
        }

        private void RefreshCardVisualMode()
        {
            var effective = GetRequestedCardVisualMode();
            foreach (var card in cards.Values)
            {
                card.UpdateVisualMode(effective);
            }
        }
    }
}
