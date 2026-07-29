namespace WeChatMessageNotifier.WinUI;

// Pure responsive-layout policy shared by the two WinUI windows. Keeping the
// thresholds outside XAML code-behind makes the production layout behavior
// testable without creating a window or loading the Windows App SDK runtime.
internal enum ResponsiveLayoutMode
{
    SingleColumn,
    TwoColumns,
    ThreeColumns
}

internal static class UiLayoutPolicy
{
    internal const double SettingsTwoColumnBreakpoint = 960;
    internal const double SettingsThreeColumnBreakpoint = 1320;
    internal const double TrayTwoColumnBreakpoint = 620;
    internal const double TrayThreeColumnBreakpoint = 980;

    internal static ResponsiveLayoutMode SelectSettingsMode(double availableWidth)
    {
        return Select(
            availableWidth,
            SettingsTwoColumnBreakpoint,
            SettingsThreeColumnBreakpoint);
    }

    internal static ResponsiveLayoutMode SelectTrayMode(double availableWidth)
    {
        return Select(
            availableWidth,
            TrayTwoColumnBreakpoint,
            TrayThreeColumnBreakpoint);
    }

    private static ResponsiveLayoutMode Select(
        double availableWidth,
        double twoColumnBreakpoint,
        double threeColumnBreakpoint)
    {
        if (availableWidth >= threeColumnBreakpoint)
        {
            return ResponsiveLayoutMode.ThreeColumns;
        }

        return availableWidth >= twoColumnBreakpoint
            ? ResponsiveLayoutMode.TwoColumns
            : ResponsiveLayoutMode.SingleColumn;
    }
}
