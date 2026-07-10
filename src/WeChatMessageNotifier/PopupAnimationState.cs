namespace WeChatMessageNotifier
{
    internal enum PopupAnimationPhase
    {
        Entering,
        Stable,
        Updating,
        Moving,
        Exiting,
        Exited
    }

    internal sealed class PopupAnimationState
    {
        private bool pulseReturning;
        private float dpiScale = 1.0f;

        internal PopupAnimationState(double layoutY, MotionMode mode)
            : this(layoutY, mode, 1.0f)
        {
        }

        internal PopupAnimationState(
            double layoutY,
            MotionMode mode,
            float dpiScale)
        {
            this.dpiScale = dpiScale <= 0 ? 1.0f : dpiScale;
            LayoutY = new AnimatedValue(layoutY);
            XOffset = new AnimatedValue(0);
            YOffset = new AnimatedValue(0);
            Scale = new AnimatedValue(
                1,
                MotionSettings.MinimumScaleDistance,
                MotionSettings.MinimumScaleVelocity);
            Opacity = new AnimatedValue(
                1,
                MotionSettings.MinimumOpacityDistance,
                MotionSettings.MinimumOpacityVelocity);
            BeginEnter(mode);
        }

        internal AnimatedValue LayoutY { get; private set; }
        internal AnimatedValue XOffset { get; private set; }
        internal AnimatedValue YOffset { get; private set; }
        internal AnimatedValue Scale { get; private set; }
        internal AnimatedValue Opacity { get; private set; }
        internal PopupAnimationPhase Phase { get; private set; }

        internal bool NeedsAnimation
        {
            get
            {
                return Phase != PopupAnimationPhase.Stable &&
                       Phase != PopupAnimationPhase.Exited ||
                       !LayoutY.IsSettled;
            }
        }

        internal void SetLayoutTarget(double target, MotionMode mode)
        {
            if (mode == MotionMode.Off)
            {
                LayoutY.Snap(target);
            }
            else
            {
                LayoutY.SetTarget(target);
                if (Phase == PopupAnimationPhase.Stable &&
                    !LayoutY.IsSettled)
                {
                    Phase = PopupAnimationPhase.Moving;
                }
            }
        }

        internal void ShiftLayoutCurrent(double delta)
        {
            LayoutY.ShiftCurrent(delta);
        }

        internal void UpdateDpiScale(float scale)
        {
            dpiScale = scale <= 0 ? 1.0f : scale;
        }

        internal void BeginEnter(MotionMode mode)
        {
            pulseReturning = true;
            if (mode == MotionMode.Off)
            {
                SnapStable();
                return;
            }

            Phase = PopupAnimationPhase.Entering;
            XOffset.Snap(ScaleMotionValue(mode == MotionMode.Reduced
                ? MotionSettings.ReducedEnterX
                : MotionSettings.StandardEnterX));
            YOffset.Snap(ScaleMotionValue(mode == MotionMode.Reduced
                ? MotionSettings.ReducedEnterY
                : MotionSettings.StandardEnterY));
            Scale.Snap(mode == MotionMode.Reduced
                ? MotionSettings.ReducedScale
                : MotionSettings.StandardEnterScale);
            Opacity.Snap(0);
            XOffset.SetTarget(0);
            YOffset.SetTarget(0);
            Scale.SetTarget(1);
            Opacity.SetTarget(1);
        }

        internal void BeginUpdate(MotionMode mode)
        {
            if (mode == MotionMode.Off)
            {
                SnapStable();
                return;
            }

            Phase = PopupAnimationPhase.Updating;
            pulseReturning = mode == MotionMode.Reduced;
            XOffset.SetTarget(0);
            YOffset.SetTarget(0);
            Opacity.Snap(
                mode == MotionMode.Reduced
                    ? MotionSettings.ReducedUpdateOpacity
                    : MotionSettings.StandardUpdateOpacity);
            Opacity.SetTarget(1);
            if (mode == MotionMode.Reduced)
            {
                Scale.Snap(1);
            }
            else
            {
                Scale.Snap(1);
                Scale.SetTarget(MotionSettings.UpdatePulseScale);
            }
        }

        internal void BeginExit(MotionMode mode)
        {
            if (mode == MotionMode.Off)
            {
                Phase = PopupAnimationPhase.Exited;
                Opacity.Snap(0);
                return;
            }

            Phase = PopupAnimationPhase.Exiting;
            pulseReturning = true;
            XOffset.SetTarget(ScaleMotionValue(mode == MotionMode.Reduced
                ? MotionSettings.ReducedExitX
                : MotionSettings.StandardExitX));
            YOffset.SetTarget(ScaleMotionValue(mode == MotionMode.Reduced
                ? MotionSettings.ReducedExitY
                : MotionSettings.StandardExitY));
            Scale.SetTarget(mode == MotionMode.Reduced
                ? 1
                : MotionSettings.StandardExitScale);
            Opacity.SetTarget(0);
        }

        internal void SnapStable()
        {
            Phase = PopupAnimationPhase.Stable;
            XOffset.Snap(0);
            YOffset.Snap(0);
            Scale.Snap(1);
            Opacity.Snap(1);
            LayoutY.Snap(LayoutY.Target);
            pulseReturning = true;
        }

        internal bool Advance(MotionMode mode)
        {
            if (mode == MotionMode.Off)
            {
                if (Phase == PopupAnimationPhase.Exiting)
                {
                    Phase = PopupAnimationPhase.Exited;
                }
                else
                {
                    SnapStable();
                }
                return false;
            }

            double stiffness;
            double damping;
            double maximumStep;
            if (mode == MotionMode.Reduced)
            {
                if (Phase == PopupAnimationPhase.Exiting)
                {
                    stiffness =
                        MotionSettings.ReducedCardExitStiffness;
                    damping =
                        MotionSettings.ReducedCardExitDamping;
                    maximumStep = ScaleMotionValue(
                        MotionSettings.ReducedCardExitMaximumStep);
                }
                else if (Phase == PopupAnimationPhase.Entering)
                {
                    stiffness =
                        MotionSettings.ReducedCardEnterStiffness;
                    damping =
                        MotionSettings.ReducedCardEnterDamping;
                    maximumStep = ScaleMotionValue(
                        MotionSettings.ReducedCardEnterMaximumStep);
                }
                else
                {
                    stiffness =
                        MotionSettings.ReducedCardMoveStiffness;
                    damping =
                        MotionSettings.ReducedCardMoveDamping;
                    maximumStep = ScaleMotionValue(
                        MotionSettings.ReducedCardMoveMaximumStep);
                }
            }
            else if (Phase == PopupAnimationPhase.Exiting)
            {
                stiffness =
                    MotionSettings.StandardCardExitStiffness;
                damping =
                    MotionSettings.StandardCardExitDamping;
                maximumStep = ScaleMotionValue(
                    MotionSettings.StandardCardExitMaximumStep);
            }
            else if (Phase == PopupAnimationPhase.Entering)
            {
                stiffness =
                    MotionSettings.StandardCardEnterStiffness;
                damping =
                    MotionSettings.StandardCardEnterDamping;
                maximumStep = ScaleMotionValue(
                    MotionSettings.StandardCardEnterMaximumStep);
            }
            else if (Phase == PopupAnimationPhase.Updating)
            {
                stiffness = MotionSettings.CardUpdateStiffness;
                damping = MotionSettings.CardUpdateDamping;
                maximumStep = ScaleMotionValue(
                    MotionSettings.StandardCardMoveMaximumStep);
            }
            else
            {
                stiffness =
                    MotionSettings.StandardCardMoveStiffness;
                damping =
                    MotionSettings.StandardCardMoveDamping;
                maximumStep = ScaleMotionValue(
                    MotionSettings.StandardCardMoveMaximumStep);
            }

            LayoutY.Advance(
                mode == MotionMode.Reduced
                    ? MotionSettings.ReducedCardMoveStiffness
                    : MotionSettings.StandardCardMoveStiffness,
                mode == MotionMode.Reduced
                    ? MotionSettings.ReducedCardMoveDamping
                    : MotionSettings.StandardCardMoveDamping,
                ScaleMotionValue(mode == MotionMode.Reduced
                    ? MotionSettings.ReducedCardMoveMaximumStep
                    : MotionSettings.StandardCardMoveMaximumStep));
            XOffset.Advance(stiffness, damping, maximumStep);
            YOffset.Advance(stiffness, damping, maximumStep);
            double opacityResponse;
            double opacityMaximumStep;
            if (Phase == PopupAnimationPhase.Exiting)
            {
                opacityResponse = mode == MotionMode.Reduced
                    ? MotionSettings.ReducedExitOpacityResponse
                    : MotionSettings.StandardExitOpacityResponse;
                opacityMaximumStep = mode == MotionMode.Reduced
                    ? MotionSettings.ReducedExitOpacityMaximumStep
                    : MotionSettings.StandardExitOpacityMaximumStep;
            }
            else if (Phase == PopupAnimationPhase.Updating)
            {
                opacityResponse =
                    MotionSettings.UpdateOpacityResponse;
                opacityMaximumStep =
                    MotionSettings.UpdateOpacityMaximumStep;
            }
            else
            {
                opacityResponse = mode == MotionMode.Reduced
                    ? MotionSettings.ReducedEnterOpacityResponse
                    : MotionSettings.StandardEnterOpacityResponse;
                opacityMaximumStep = mode == MotionMode.Reduced
                    ? MotionSettings.ReducedEnterOpacityMaximumStep
                    : MotionSettings.StandardEnterOpacityMaximumStep;
            }
            Opacity.AdvanceMonotonic(
                opacityResponse,
                opacityMaximumStep);
            Scale.Advance(
                stiffness,
                damping,
                MotionSettings.CardScaleMaximumStep);

            if (Phase == PopupAnimationPhase.Updating &&
                !pulseReturning &&
                Scale.IsSettled)
            {
                pulseReturning = true;
                Scale.SetTarget(1);
                return true;
            }

            var visualSettled =
                XOffset.IsSettled &&
                YOffset.IsSettled &&
                Opacity.IsSettled &&
                Scale.IsSettled;
            if (visualSettled)
            {
                if (Phase == PopupAnimationPhase.Exiting)
                {
                    Phase = PopupAnimationPhase.Exited;
                }
                else if (Phase != PopupAnimationPhase.Stable &&
                         pulseReturning &&
                         LayoutY.IsSettled)
                {
                    Phase = PopupAnimationPhase.Stable;
                }
            }

            return NeedsAnimation;
        }

        private double ScaleMotionValue(double value)
        {
            return value * dpiScale;
        }
    }
}
