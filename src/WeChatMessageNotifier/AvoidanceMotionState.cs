namespace WeChatMessageNotifier
{
    // Keeps low-frequency obstruction detection separate from the 60 FPS
    // movement. Detection changes only Target; animation advances Current and
    // preserves Velocity when a tray panel changes size while already moving.
    internal sealed class AvoidanceMotionState
    {
        private readonly AnimatedValue offset = new AnimatedValue(
            0,
            MotionSettings.AvoidanceMinimumDistance,
            MotionSettings.AvoidanceMinimumVelocity);
        private int clearSamples;
        private int lowerSamples;
        private int pendingLowerOffset;
        private int settledFrames =
            MotionSettings.AvoidanceStableFrameCount;

        internal double Current
        {
            get { return offset.Current; }
        }

        internal double Target
        {
            get { return offset.Target; }
        }

        internal double Velocity
        {
            get { return offset.Velocity; }
        }

        internal bool IsSettled
        {
            get
            {
                return offset.IsSettled &&
                       settledFrames >=
                       MotionSettings.AvoidanceStableFrameCount;
            }
        }

        internal bool ReportDetection(
            int detectedOffset,
            MotionMode mode)
        {
            detectedOffset = detectedOffset < 0 ? 0 : detectedOffset;
            var target = (int)System.Math.Round(offset.Target);
            if (detectedOffset == 0)
            {
                lowerSamples = 0;
                pendingLowerOffset = 0;
                if (target == 0)
                {
                    clearSamples = 0;
                    return false;
                }

                clearSamples++;
                if (clearSamples <
                    MotionSettings.ObstructionClearConfirmationSamples)
                {
                    return false;
                }
                clearSamples = 0;
                return SetTarget(0, mode);
            }

            clearSamples = 0;
            if (target == 0 ||
                detectedOffset >
                target + MotionSettings.ObstructionTargetTolerance)
            {
                lowerSamples = 0;
                pendingLowerOffset = detectedOffset;
                return SetTarget(detectedOffset, mode);
            }

            if (System.Math.Abs(detectedOffset - target) <=
                MotionSettings.ObstructionTargetTolerance)
            {
                lowerSamples = 0;
                pendingLowerOffset = detectedOffset;
                return false;
            }

            if (System.Math.Abs(
                    detectedOffset - pendingLowerOffset) >
                MotionSettings.ObstructionTargetTolerance)
            {
                pendingLowerOffset = detectedOffset;
                lowerSamples = 1;
                return false;
            }

            lowerSamples++;
            if (lowerSamples <
                MotionSettings.ObstructionLowerConfirmationSamples)
            {
                return false;
            }
            lowerSamples = 0;
            return SetTarget(detectedOffset, mode);
        }

        internal bool Advance(MotionMode mode)
        {
            if (mode == MotionMode.Off)
            {
                SnapToTarget();
                return false;
            }

            offset.Advance(
                mode == MotionMode.Reduced
                    ? MotionSettings.ReducedAvoidanceStiffness
                    : MotionSettings.StandardAvoidanceStiffness,
                mode == MotionMode.Reduced
                    ? MotionSettings.ReducedAvoidanceDamping
                    : MotionSettings.StandardAvoidanceDamping,
                mode == MotionMode.Reduced
                    ? MotionSettings.ReducedAvoidanceMaximumStep
                    : MotionSettings.StandardAvoidanceMaximumStep);
            if (offset.IsSettled)
            {
                settledFrames++;
                if (settledFrames >=
                    MotionSettings.AvoidanceStableFrameCount)
                {
                    offset.Snap(offset.Target);
                    return false;
                }
            }
            else
            {
                settledFrames = 0;
            }
            return true;
        }

        internal void SnapToTarget()
        {
            offset.Snap(offset.Target);
            settledFrames =
                MotionSettings.AvoidanceStableFrameCount;
        }

        internal void Reset()
        {
            offset.Snap(0);
            clearSamples = 0;
            lowerSamples = 0;
            pendingLowerOffset = 0;
            settledFrames =
                MotionSettings.AvoidanceStableFrameCount;
        }

        private bool SetTarget(int target, MotionMode mode)
        {
            if (System.Math.Abs(offset.Target - target) <
                MotionSettings.AvoidanceMinimumDistance)
            {
                return false;
            }

            if (mode == MotionMode.Off)
            {
                offset.Snap(target);
                settledFrames =
                    MotionSettings.AvoidanceStableFrameCount;
            }
            else
            {
                // Deliberately keep the existing velocity. Resetting it on
                // every 250 ms detection causes visible staircase motion.
                offset.SetTarget(target);
                settledFrames = 0;
            }
            return true;
        }
    }
}
