using System;
using System.IO;
using Microsoft.UI.Xaml;

namespace WeChatMessageNotifier.WinUI;

public partial class App : Application
{
    // WinUI owns windows through managed references. Keeping the settings
    // window here prevents a GC collection from tearing down the XAML host
    // after OnLaunched returns.
    private Window? activeWindow;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var arguments = SettingsLaunchArguments.ParseCommandLine(args.Arguments);
            activeWindow = arguments.IsTrayMenu
                ? new TrayWindow(arguments)
                : new SettingsWindow(arguments);
            activeWindow.Activate();
        }
        catch (Exception exception)
        {
            // Keep a short local diagnostic while the new UI is being
            // initialized. It records only the exception, never settings or
            // message content, and makes startup failures actionable.
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "WeChatMessageNotifier.WinUI.startup-error.txt"),
                exception.ToString());
            throw;
        }
    }
}
