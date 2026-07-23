using System;
using System.IO;
using System.Text;

namespace WeChatMessageNotifier.WinUI;

// A one-way local command file keeps the modern UI independent from the
// .NET Framework monitor.  It contains only a fixed command name, never
// contacts, message text, or notification data.
internal static class TrayCommandStore
{
    internal static void Write(string path, string command)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A local command path is required.", nameof(path));
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporaryPath, command ?? string.Empty, new UTF8Encoding(false));
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        File.Move(temporaryPath, path);
    }
}
