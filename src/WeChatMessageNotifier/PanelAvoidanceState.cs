namespace WeChatMessageNotifier
{
    // Two independent axes let Quick Settings move the host upward while
    // Notification Center moves it left. Card animation remains local to host.
    internal sealed class PanelAvoidanceState
    {
        private readonly AnimatedValue x = new AnimatedValue(
            0, MotionSettings.AvoidanceMinimumDistance, MotionSettings.AvoidanceMinimumVelocity);
        private readonly AnimatedValue y = new AnimatedValue(
            0, MotionSettings.AvoidanceMinimumDistance, MotionSettings.AvoidanceMinimumVelocity);

        internal double CurrentOffsetX { get { return x.Current; } }
        internal double CurrentOffsetY { get { return y.Current; } }
        internal double TargetOffsetX { get { return x.Target; } }
        internal double TargetOffsetY { get { return y.Target; } }
        internal double VelocityX { get { return x.Velocity; } }
        internal double VelocityY { get { return y.Velocity; } }
        internal bool IsSettled { get { return x.IsSettled && y.IsSettled; } }

        internal bool SetTargets(double targetX, double targetY, MotionMode mode)
        {
            var changed = System.Math.Abs(x.Target - targetX) >= MotionSettings.AvoidanceMinimumDistance ||
                          System.Math.Abs(y.Target - targetY) >= MotionSettings.AvoidanceMinimumDistance;
            if (!changed)
            {
                return false;
            }
            if (mode == MotionMode.Off)
            {
                x.Snap(targetX);
                y.Snap(targetY);
            }
            else
            {
                x.SetTarget(targetX);
                y.SetTarget(targetY);
            }
            return true;
        }

        internal bool Advance(MotionMode mode)
        {
            if (mode == MotionMode.Off)
            {
                x.Snap(x.Target);
                y.Snap(y.Target);
                return false;
            }
            var stiffness = mode == MotionMode.Reduced
                ? MotionSettings.ReducedAvoidanceStiffness
                : MotionSettings.StandardAvoidanceStiffness;
            var damping = mode == MotionMode.Reduced
                ? MotionSettings.ReducedAvoidanceDamping
                : MotionSettings.StandardAvoidanceDamping;
            var maximumStep = mode == MotionMode.Reduced
                ? MotionSettings.ReducedAvoidanceMaximumStep
                : MotionSettings.StandardAvoidanceMaximumStep;
            var xRunning = x.Advance(stiffness, damping, maximumStep);
            var yRunning = y.Advance(stiffness, damping, maximumStep);
            return xRunning || yRunning;
        }

        internal void Reset()
        {
            x.Snap(0);
            y.Snap(0);
        }

        internal void SnapToTarget()
        {
            x.Snap(x.Target);
            y.Snap(y.Target);
        }
    }
}
