using System;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace WeChatMessageNotifier.WinUI;

public sealed partial class TrayWindow : Window
{
    private enum MenuLayoutMode
    {
        SingleColumn,
        TwoColumns,
        ThreeColumns
    }

    private const int MenuWidth = 338;
    private const int MenuHeight = 510;
    private const double TwoColumnBreakpoint = 620;
    private const double ThreeColumnBreakpoint = 980;
    private readonly SettingsLaunchArguments arguments;
    private readonly AppWindow appWindow;
    private MenuLayoutMode? menuLayoutMode;

    internal TrayWindow(SettingsLaunchArguments arguments)
    {
        this.arguments = arguments;
        InitializeComponent();

        var hwnd = WindowNative.GetWindowHandle(this);
        appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
        appWindow.Resize(new SizeInt32(MenuWidth, MenuHeight));
        appWindow.Move(new PointInt32(arguments.X - MenuWidth, arguments.Y - MenuHeight));
        ApplyWindowIcon();
        SystemBackdrop = new DesktopAcrylicBackdrop();
        ApplyTransparentTitleBar();
        MenuRoot.SizeChanged += MenuRoot_SizeChanged;
        StatusText.Text = string.IsNullOrWhiteSpace(arguments.MonitoringState)
            ? "状态：本地提醒器运行中"
            : arguments.MonitoringState;
        PauseIcon.Symbol = arguments.IsPaused ? Symbol.Play : Symbol.Pause;
        PauseText.Text = arguments.IsPaused ? "继续监控" : "暂停监控";
        PrivacyText.Text = arguments.PrivacyMode
            ? "隐私模式（仅显示联系人） ✓"
            : "隐私模式（显示消息摘要）";
        ApplyResponsiveMenuLayout(MenuWidth);
    }

    private void CommandButton_Click(object sender, RoutedEventArgs args)
    {
        var button = sender as Button;
        var command = button == null ? null : button.Tag as string;
        if (!string.IsNullOrWhiteSpace(command))
        {
            TrayCommandStore.Write(this.arguments.CommandPath, command);
        }
        Close();
    }

    private void ApplyTransparentTitleBar()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = appWindow.TitleBar;
        titleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.InactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
    }

    private void ApplyWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "WeChatMessageNotifier.WinUI.ico");
        if (File.Exists(iconPath))
        {
            appWindow.SetIcon(iconPath);
        }
    }

    private void MenuRoot_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        ApplyResponsiveMenuLayout(args.NewSize.Width);
    }

    // Keep the compact tray menu quick to scan, but use the available width
    // when the user expands the window. All controls are created once in XAML;
    // resizing only changes their Grid positions.
    private void ApplyResponsiveMenuLayout(double availableWidth)
    {
        var targetMode = availableWidth >= ThreeColumnBreakpoint
            ? MenuLayoutMode.ThreeColumns
            : availableWidth >= TwoColumnBreakpoint
                ? MenuLayoutMode.TwoColumns
                : MenuLayoutMode.SingleColumn;
        if (menuLayoutMode == targetMode)
        {
            return;
        }

        menuLayoutMode = targetMode;
        MenuLayout.ColumnDefinitions.Clear();
        MenuLayout.RowDefinitions.Clear();

        switch (targetMode)
        {
            case MenuLayoutMode.ThreeColumns:
                AddColumns(3);
                AddRows(3);
                PlaceElement(StatusPanel, 0, 0, 3);
                PlaceElement(OpenSettingsButton, 1, 0, 1);
                PlaceElement(PauseButton, 1, 1, 1);
                PlaceElement(PrivacyButton, 1, 2, 1);
                PlaceElement(NotificationExpander, 2, 0, 1);
                PlaceElement(DiagnosticExpander, 2, 1, 1);
                PlaceElement(ExitButton, 2, 2, 1);
                break;

            case MenuLayoutMode.TwoColumns:
                AddColumns(2);
                AddRows(5);
                PlaceElement(StatusPanel, 0, 0, 2);
                PlaceElement(OpenSettingsButton, 1, 0, 1);
                PlaceElement(PauseButton, 1, 1, 1);
                PlaceElement(PrivacyButton, 2, 0, 2);
                PlaceElement(NotificationExpander, 3, 0, 1);
                PlaceElement(DiagnosticExpander, 3, 1, 1);
                PlaceElement(ExitButton, 4, 0, 2);
                break;

            default:
                AddColumns(1);
                AddRows(7);
                PlaceElement(StatusPanel, 0, 0, 1);
                PlaceElement(OpenSettingsButton, 1, 0, 1);
                PlaceElement(PauseButton, 2, 0, 1);
                PlaceElement(PrivacyButton, 3, 0, 1);
                PlaceElement(NotificationExpander, 4, 0, 1);
                PlaceElement(DiagnosticExpander, 5, 0, 1);
                PlaceElement(ExitButton, 6, 0, 1);
                break;
        }
    }

    private void AddColumns(int count)
    {
        for (var index = 0; index < count; index++)
        {
            MenuLayout.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
        }
    }

    private void AddRows(int count)
    {
        for (var index = 0; index < count; index++)
        {
            MenuLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
    }

    private static void PlaceElement(FrameworkElement element, int row, int column, int columnSpan)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, columnSpan);
    }
}
