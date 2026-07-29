using System.Text;

namespace WeChatMessageNotifier.WinUI;

internal static class Program
{
    private static int Main()
    {
        var failures = new List<string>();
        TestLaunchArguments(failures);
        TestResponsiveLayoutPolicy(failures);
        TestSettingsPersistence(failures);
        TestLocalCommandWrite(failures);

        if (failures.Count == 0)
        {
            Console.WriteLine("WINUI SELF-TEST PASSED");
            return 0;
        }

        Console.WriteLine("WINUI SELF-TEST FAILED");
        foreach (var failure in failures)
        {
            Console.WriteLine("- " + failure);
        }
        return 1;
    }

    private static void TestLaunchArguments(ICollection<string> failures)
    {
        var parsed = SettingsLaunchArguments.Parse(
            "--tray --settings-path \"C:\\Local Data\\settings.json\" " +
            "--monitoring-state \"状态：已连接微信 · 11 个会话\" " +
            "--command-path \"C:\\Local Data\\command.txt\" " +
            "--x 1400 --y 900 --paused --privacy");

        Expect(parsed.IsTrayMenu && parsed.IsPaused && parsed.PrivacyMode &&
               parsed.X == 1400 && parsed.Y == 900 &&
               parsed.SettingsPath.EndsWith("settings.json", StringComparison.Ordinal) &&
               parsed.CommandPath.EndsWith("command.txt", StringComparison.Ordinal) &&
               parsed.MonitoringState.IndexOf("11", StringComparison.Ordinal) >= 0,
            "WinUI launch arguments did not preserve quoted local values.", failures);
    }

    private static void TestResponsiveLayoutPolicy(ICollection<string> failures)
    {
        Expect(UiLayoutPolicy.SelectSettingsMode(959) == ResponsiveLayoutMode.SingleColumn &&
               UiLayoutPolicy.SelectSettingsMode(960) == ResponsiveLayoutMode.TwoColumns &&
               UiLayoutPolicy.SelectSettingsMode(1320) == ResponsiveLayoutMode.ThreeColumns,
            "Settings responsive layout breakpoints regressed.", failures);
        Expect(UiLayoutPolicy.SelectTrayMode(619) == ResponsiveLayoutMode.SingleColumn &&
               UiLayoutPolicy.SelectTrayMode(620) == ResponsiveLayoutMode.TwoColumns &&
               UiLayoutPolicy.SelectTrayMode(980) == ResponsiveLayoutMode.ThreeColumns,
            "Tray responsive layout breakpoints regressed.", failures);
    }

    private static void TestSettingsPersistence(ICollection<string> failures)
    {
        var directory = Path.Combine(Path.GetTempPath(), "WeChatMessageNotifier-WinUiTest-" + Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                settingsPath,
                "{\"sessionKindOverrides\":{\"a1b2c3d4e5f60708\":\"GroupChat\"},\"untouched\":true}",
                new UTF8Encoding(false));

            var document = new SettingsDocument(settingsPath);
            document.Load();
            document.SetValues(
                "医院\r博物馆\r\n机场\n医院\n ",
                "机场\n移动",
                "团购\n福利",
                true);
            document.SaveAtomically();

            var reloaded = new SettingsDocument(settingsPath);
            reloaded.Load();
            var persistedSettings = File.ReadAllText(settingsPath, Encoding.UTF8);
            Expect(reloaded.PrivacyMode &&
                   reloaded.ServiceAccountAllowKeywords.SequenceEqual(new[] { "医院", "博物馆", "机场" }) &&
                   reloaded.OfficialAccountAllowKeywords.SequenceEqual(new[] { "机场", "移动" }) &&
                   reloaded.GroupChatBlockKeywords.SequenceEqual(new[] { "团购", "福利" }) &&
                   persistedSettings.IndexOf("sessionKindOverrides", StringComparison.Ordinal) >= 0 &&
                   persistedSettings.IndexOf("医院", StringComparison.Ordinal) >= 0 &&
                   persistedSettings.IndexOf("\\u533b", StringComparison.OrdinalIgnoreCase) < 0,
                "WinUI settings save did not preserve normalized rules and unrelated local fields.", failures);

            document.SetValues("银行", "医院", "优惠", false);
            document.SaveAtomically();
            Expect(File.Exists(settingsPath + ".bak"),
                "WinUI settings save did not retain a rollback backup.", failures);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static void TestLocalCommandWrite(ICollection<string> failures)
    {
        var directory = Path.Combine(Path.GetTempPath(), "WeChatMessageNotifier-WinUiCommand-" + Guid.NewGuid().ToString("N"));
        var commandPath = Path.Combine(directory, "ui-command.txt");
        try
        {
            TrayCommandStore.Write(commandPath, "open-settings");
            Expect(File.ReadAllText(commandPath, Encoding.UTF8) == "open-settings" &&
                   Directory.GetFiles(directory, "*.tmp").Length == 0,
                "WinUI command bridge did not write one complete local command.", failures);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static void Expect(bool condition, string message, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }
}
