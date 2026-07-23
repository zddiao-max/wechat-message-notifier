using System;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace WeChatMessageNotifier.WinUI;

public sealed partial class SettingsWindow : Window
{
    private enum CardsLayoutMode
    {
        SingleColumn,
        TwoColumns,
        ThreeColumns
    }

    private const double TwoColumnBreakpoint = 960;
    private const double ThreeColumnBreakpoint = 1320;
    private readonly SettingsLaunchArguments launchArguments;
    private readonly SettingsDocument settings;
    private readonly AppWindow appWindow;
    private CardsLayoutMode? cardsLayoutMode;

    internal SettingsWindow(SettingsLaunchArguments launchArguments)
    {
        this.launchArguments = launchArguments;
        settings = new SettingsDocument(launchArguments.SettingsPath);
        InitializeComponent();

        var hwnd = WindowNative.GetWindowHandle(this);
        appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
        appWindow.Resize(new SizeInt32(920, 760));

        var iconPath = Path.Combine(AppContext.BaseDirectory, "WeChatMessageNotifier.WinUI.ico");
        if (File.Exists(iconPath))
        {
            appWindow.SetIcon(iconPath);
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = MicaController.IsSupported()
            ? new MicaBackdrop { Kind = MicaKind.BaseAlt }
            : new DesktopAcrylicBackdrop();

        RootGrid.SizeChanged += RootGrid_SizeChanged;
        LoadSettings();
        ApplyResponsiveCardsLayout(RootGrid.ActualWidth);
    }

    private void LoadSettings()
    {
        try
        {
            settings.Load();
            ServiceKeywordsTextBox.Text = string.Join(Environment.NewLine, settings.ServiceAccountAllowKeywords);
            OfficialKeywordsTextBox.Text = string.Join(Environment.NewLine, settings.OfficialAccountAllowKeywords);
            GroupKeywordsTextBox.Text = string.Join(Environment.NewLine, settings.GroupChatBlockKeywords);
            PrivacyModeToggle.IsOn = settings.PrivacyMode;
            StateInfoBar.Message = string.IsNullOrWhiteSpace(launchArguments.MonitoringState)
                ? "设置已从本地文件读取"
                : launchArguments.MonitoringState;
        }
        catch (Exception exception)
        {
            StateInfoBar.Severity = InfoBarSeverity.Error;
            StateInfoBar.Title = "无法读取设置";
            StateInfoBar.Message = exception.Message;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            settings.SetValues(
                ServiceKeywordsTextBox.Text,
                OfficialKeywordsTextBox.Text,
                GroupKeywordsTextBox.Text,
                PrivacyModeToggle.IsOn);
            settings.SaveAtomically();
            SaveHintText.Text = "已保存。提醒器会在下一次轮询中读取新规则。";
            StateInfoBar.Severity = InfoBarSeverity.Success;
            StateInfoBar.Title = "已保存";
            StateInfoBar.Message = "规则已写入本地 settings.json。";
        }
        catch (Exception exception)
        {
            StateInfoBar.Severity = InfoBarSeverity.Error;
            StateInfoBar.Title = "保存失败";
            StateInfoBar.Message = exception.Message;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveCardsLayout(e.NewSize.Width);
    }

    // WinUI measures in device-independent pixels, so these breakpoints remain
    // readable on 100% through 200% monitors. Existing controls move between
    // Grid cells; no card or editor is recreated while the user resizes.
    private void ApplyResponsiveCardsLayout(double availableWidth)
    {
        var targetMode = availableWidth >= ThreeColumnBreakpoint
            ? CardsLayoutMode.ThreeColumns
            : availableWidth >= TwoColumnBreakpoint
                ? CardsLayoutMode.TwoColumns
                : CardsLayoutMode.SingleColumn;
        if (cardsLayoutMode == targetMode)
        {
            return;
        }

        cardsLayoutMode = targetMode;
        CardsGrid.ColumnDefinitions.Clear();
        CardsGrid.RowDefinitions.Clear();

        switch (targetMode)
        {
            case CardsLayoutMode.ThreeColumns:
                AddColumns(3);
                AddRows(1);
                PlaceCard(ServiceKeywordsCard, 0, 0, 1);
                PlaceCard(OfficialKeywordsCard, 0, 1, 1);
                PlaceCard(GroupKeywordsCard, 0, 2, 1);
                break;

            case CardsLayoutMode.TwoColumns:
                AddColumns(2);
                AddRows(2);
                PlaceCard(ServiceKeywordsCard, 0, 0, 1);
                PlaceCard(OfficialKeywordsCard, 0, 1, 1);
                PlaceCard(GroupKeywordsCard, 1, 0, 2);
                break;

            default:
                AddColumns(1);
                AddRows(3);
                PlaceCard(ServiceKeywordsCard, 0, 0, 1);
                PlaceCard(OfficialKeywordsCard, 1, 0, 1);
                PlaceCard(GroupKeywordsCard, 2, 0, 1);
                break;
        }
    }

    private void AddColumns(int count)
    {
        for (var index = 0; index < count; index++)
        {
            CardsGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
        }
    }

    private void AddRows(int count)
    {
        for (var index = 0; index < count; index++)
        {
            CardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
    }

    private static void PlaceCard(FrameworkElement card, int row, int column, int columnSpan)
    {
        Grid.SetRow(card, row);
        Grid.SetColumn(card, column);
        Grid.SetColumnSpan(card, columnSpan);
    }
}
