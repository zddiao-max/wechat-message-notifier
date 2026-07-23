using System;
using System.Collections.Generic;
using System.IO;

namespace WeChatMessageNotifier.WinUI;

internal sealed class SettingsLaunchArguments
{
    internal string SettingsPath { get; private init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WeChatMessageNotifier",
        "settings.json");

    internal string MonitoringState { get; private init; } = "状态：由本地提醒器管理";

    internal bool IsTrayMenu { get; private init; }

    internal string CommandPath { get; private init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WeChatMessageNotifier",
        "ui-command.txt");

    internal int X { get; private init; }

    internal int Y { get; private init; }

    internal bool IsPaused { get; private init; }

    internal bool PrivacyMode { get; private init; }

    internal static SettingsLaunchArguments Parse(string? arguments)
    {
        return ParseValues(Tokenize(arguments));
    }

    // Windows App SDK can surface an empty LaunchActivatedEventArgs.Arguments
    // for an unpackaged EXE. Environment.GetCommandLineArgs remains the
    // authoritative source in that case and preserves the already-tokenized
    // file paths supplied by the .NET Framework tray host.
    internal static SettingsLaunchArguments ParseCommandLine(string? activationArguments)
    {
        var commandLine = Environment.GetCommandLineArgs();
        var values = new List<string>();
        for (var index = 1; index < commandLine.Length; index++)
        {
            values.Add(commandLine[index]);
        }
        return values.Count > 0
            ? ParseValues(values)
            : Parse(activationArguments);
    }

    private static SettingsLaunchArguments ParseValues(IReadOnlyList<string> values)
    {
        var settingsPath = GetValue(values, "--settings-path");
        var monitoringState = GetValue(values, "--monitoring-state");
        var commandPath = GetValue(values, "--command-path");
        return new SettingsLaunchArguments
        {
            SettingsPath = string.IsNullOrWhiteSpace(settingsPath)
                ? new SettingsLaunchArguments().SettingsPath
                : settingsPath,
            MonitoringState = string.IsNullOrWhiteSpace(monitoringState)
                ? new SettingsLaunchArguments().MonitoringState
                : monitoringState,
            IsTrayMenu = HasFlag(values, "--tray"),
            CommandPath = string.IsNullOrWhiteSpace(commandPath)
                ? new SettingsLaunchArguments().CommandPath
                : commandPath,
            X = GetIntValue(values, "--x"),
            Y = GetIntValue(values, "--y"),
            IsPaused = HasFlag(values, "--paused"),
            PrivacyMode = HasFlag(values, "--privacy")
        };
    }

    private static string? GetValue(IReadOnlyList<string> values, string key)
    {
        for (var index = 0; index + 1 < values.Count; index++)
        {
            if (string.Equals(values[index], key, StringComparison.OrdinalIgnoreCase))
            {
                return values[index + 1];
            }
        }
        return null;
    }

    private static bool HasFlag(IReadOnlyList<string> values, string flag)
    {
        foreach (var value in values)
        {
            if (string.Equals(value, flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static int GetIntValue(IReadOnlyList<string> values, string key)
    {
        var value = GetValue(values, key);
        int parsed;
        return int.TryParse(value, out parsed) ? parsed : 0;
    }

    private static List<string> Tokenize(string? input)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(input)) return result;
        var current = new System.Text.StringBuilder();
        var quoted = false;
        foreach (var character in input)
        {
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (char.IsWhiteSpace(character) && !quoted)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }
            current.Append(character);
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }
}
