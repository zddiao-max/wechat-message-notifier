using System;

namespace WeChatMessageNotifier
{
    internal sealed class AnimatedValue
    {
        private readonly double minimumDistance;
        private readonly double minimumVelocity;

        internal AnimatedValue(double value)
            : this(
                value,
                MotionSettings.MinimumDistance,
                MotionSettings.MinimumVelocity)
        {
        }

        internal AnimatedValue(
            double value,
            double minimumDistance,
            double minimumVelocity)
        {
            Current = value;
            Target = value;
            this.minimumDistance = minimumDistance;
            this.minimumVelocity = minimumVelocity;
        }

        internal double Current { get; private set; }

        internal double Target { get; private set; }

        internal double Velocity { get; private set; }

        internal bool IsSettled
        {
            get
            {
                return Math.Abs(Target - Current) <
                       minimumDistance &&
                       Math.Abs(Velocity) <
                       minimumVelocity;
            }
        }

        internal void SetTarget(double target)
        {
            Target = target;
        }

        internal void Snap(double value)
        {
            Current = value;
            Target = value;
            Velocity = 0;
        }

        internal void ShiftCurrent(double delta)
        {
            Current += delta;
        }

        internal bool Advance(double stiffness, double damping)
        {
            return Advance(
                stiffness,
                damping,
                double.PositiveInfinity);
        }

        internal bool Advance(
            double stiffness,
            double damping,
            double maximumStep)
        {
            Velocity = (Velocity + (Target - Current) * stiffness) * damping;
            if (!double.IsPositiveInfinity(maximumStep))
            {
                Velocity = Math.Max(
                    -maximumStep,
                    Math.Min(maximumStep, Velocity));
            }
            Current += Velocity;
            if (IsSettled)
            {
                Current = Target;
                Velocity = 0;
                return false;
            }
            return true;
        }

        internal bool AdvanceMonotonic(
            double response,
            double maximumStep)
        {
            var remaining = Target - Current;
            if (Math.Abs(remaining) < minimumDistance)
            {
                Snap(Target);
                return false;
            }

            var step = remaining * response;
            step = Math.Max(
                -maximumStep,
                Math.Min(maximumStep, step));
            if (Math.Abs(step) > Math.Abs(remaining))
            {
                step = remaining;
            }

            Velocity = step;
            Current += step;
            if (Math.Abs(Target - Current) < minimumDistance)
            {
                Snap(Target);
                return false;
            }
            return true;
        }
    }
}
