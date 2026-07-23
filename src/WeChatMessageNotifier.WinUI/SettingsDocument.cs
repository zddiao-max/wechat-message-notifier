using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WeChatMessageNotifier.WinUI;

// The UI owns only the three keyword lists and privacy switch.  Everything
// else is preserved verbatim so classifier overrides and future backend
// settings survive a UI update unchanged.
internal sealed class SettingsDocument
{
    private readonly string path;
    private JsonObject root;

    internal SettingsDocument(string path)
    {
        this.path = path;
        root = new JsonObject();
    }

    internal IReadOnlyList<string> ServiceAccountAllowKeywords
    {
        get { return ReadKeywords("serviceAccountAllowKeywords"); }
    }

    internal IReadOnlyList<string> OfficialAccountAllowKeywords
    {
        get { return ReadKeywords("officialAccountAllowKeywords"); }
    }

    internal IReadOnlyList<string> GroupChatBlockKeywords
    {
        get { return ReadKeywords("groupChatBlockKeywords"); }
    }

    internal bool PrivacyMode
    {
        get
        {
            return root["privacyMode"] is JsonValue value &&
                value.TryGetValue<bool>(out var parsed) && parsed;
        }
    }

    internal void Load()
    {
        if (!File.Exists(path))
        {
            root = new JsonObject();
            return;
        }

        var parsed = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)) as JsonObject;
        root = parsed ?? new JsonObject();
    }

    internal void SetValues(
        string serviceAccountKeywords,
        string officialAccountKeywords,
        string groupChatKeywords,
        bool privacyMode)
    {
        root["serviceAccountAllowKeywords"] = ToJsonArray(serviceAccountKeywords);
        root["officialAccountAllowKeywords"] = ToJsonArray(officialAccountKeywords);
        root["groupChatBlockKeywords"] = ToJsonArray(groupChatKeywords);
        root["privacyMode"] = privacyMode;
    }

    internal void SaveAtomically()
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var backupPath = path + ".bak";
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(temporaryPath, root.ToJsonString(options), new UTF8Encoding(false));
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temporaryPath, path, backupPath, true);
                }
                catch (IOException)
                {
                    File.Copy(path, backupPath, true);
                    File.Copy(temporaryPath, path, true);
                    File.Delete(temporaryPath);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(path, backupPath, true);
                    File.Copy(temporaryPath, path, true);
                    File.Delete(temporaryPath);
                }
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private IReadOnlyList<string> ReadKeywords(string propertyName)
    {
        var value = root[propertyName] as JsonArray;
        return value == null
            ? Array.Empty<string>()
            : value
                .OfType<JsonValue>()
                .Select(item => item.TryGetValue<string>(out var text) ? text : null)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim())
                .ToArray();
    }

    private static JsonArray ToJsonArray(string text)
    {
        var values = new JsonArray();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in (text ?? string.Empty)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n'))
        {
            var keyword = line.Trim();
            if (keyword.Length > 0 && known.Add(keyword))
            {
                values.Add(keyword);
            }
        }
        return values;
    }
}
