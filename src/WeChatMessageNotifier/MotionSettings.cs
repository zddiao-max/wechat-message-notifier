namespace WeChatMessageNotifier
{
    internal static class MotionSettings
    {
        internal const int FrameIntervalMilliseconds = 16;
        internal const int ObstructionIntervalMilliseconds = 250;
        internal const int ObstructionClearConfirmationSamples = 3;
        internal const int ObstructionLowerConfirmationSamples = 2;
        internal const int ObstructionTargetTolerance = 4;
        internal const int AvoidanceStableFrameCount = 3;

        // Avoidance uses a softer, separately tuned spring than card reflow.
        // Standard permits at most a tiny overshoot; Reduced is more direct.
        internal const double StandardAvoidanceStiffness = 0.08;
        internal const double StandardAvoidanceDamping = 0.65;
        internal const double StandardAvoidanceMaximumStep = 28;
        internal const double ReducedAvoidanceStiffness = 0.14;
        internal const double ReducedAvoidanceDamping = 0.55;
        internal const double ReducedAvoidanceMaximumStep = 24;
        internal const double AvoidanceMinimumDistance = 0.5;
        internal const double AvoidanceMinimumVelocity = 0.5;

        internal const double StandardCardEnterStiffness = 0.12;
        internal const double StandardCardEnterDamping = 0.58;
        internal const double StandardCardEnterMaximumStep = 8;
        internal const double ReducedCardEnterStiffness = 0.18;
        internal const double ReducedCardEnterDamping = 0.50;
        internal const double ReducedCardEnterMaximumStep = 9;

        internal const double StandardCardMoveStiffness = 0.08;
        internal const double StandardCardMoveDamping = 0.65;
        internal const double StandardCardMoveMaximumStep = 14;
        internal const double ReducedCardMoveStiffness = 0.14;
        internal const double ReducedCardMoveDamping = 0.55;
        internal const double ReducedCardMoveMaximumStep = 16;

        internal const double StandardCardExitStiffness = 0.18;
        internal const double StandardCardExitDamping = 0.55;
        internal const double StandardCardExitMaximumStep = 14;
        internal const double ReducedCardExitStiffness = 0.22;
        internal const double ReducedCardExitDamping = 0.48;
        internal const double ReducedCardExitMaximumStep = 16;

        internal const double CardUpdateStiffness = 0.18;
        internal const double CardUpdateDamping = 0.55;
        internal const double CardScaleMaximumStep = 0.004;
        internal const double StandardEnterOpacityResponse = 0.28;
        internal const double StandardEnterOpacityMaximumStep = 0.13;
        internal const double ReducedEnterOpacityResponse = 0.38;
        internal const double ReducedEnterOpacityMaximumStep = 0.20;
        internal const double UpdateOpacityResponse = 0.30;
        internal const double UpdateOpacityMaximumStep = 0.025;
        internal const double StandardExitOpacityResponse = 0.32;
        internal const double StandardExitOpacityMaximumStep = 0.16;
        internal const double ReducedExitOpacityResponse = 0.42;
        internal const double ReducedExitOpacityMaximumStep = 0.22;

        internal const double StandardEnterScale = 0.97;
        internal const double StandardExitScale = 0.98;
        internal const double UpdatePulseScale = 1.015;
        internal const double StandardUpdateOpacity = 0.94;
        internal const double ReducedUpdateOpacity = 0.97;
        internal const double ReducedScale = 1.0;
        internal const int StandardEnterX = 24;
        internal const int StandardEnterY = 32;
        internal const int ReducedEnterX = 12;
        internal const int ReducedEnterY = 14;
        internal const int StandardExitX = 26;
        internal const int StandardExitY = 12;
        internal const int ReducedExitX = 12;
        internal const int ReducedExitY = 8;
        internal const double MinimumDistance = 0.35;
        internal const double MinimumVelocity = 0.35;
        internal const double MinimumOpacityDistance = 0.008;
        internal const double MinimumOpacityVelocity = 0.008;
        internal const double MinimumScaleDistance = 0.0008;
        internal const double MinimumScaleVelocity = 0.0008;
    }
}
