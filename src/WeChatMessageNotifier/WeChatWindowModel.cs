using System;
using System.Drawing;

namespace WeChatMessageNotifier
{
    internal enum WeChatWindowRole
    {
        MainShell,
        DetachedChat,
        Auxiliary,
        Unknown
    }

    internal enum WeChatPageKind
    {
        MainSessions,
        ServiceAccountList,
        ServiceAccountDetail,
        Conversation,
        Unknown
    }

    internal sealed class WeChatWindowSnapshot
    {
        internal IntPtr Handle { get; set; }
        internal int ProcessId { get; set; }
        internal string WindowClass { get; set; }
        internal Rectangle Bounds { get; set; }
        internal bool IsVisible { get; set; }
        internal bool IsIconic { get; set; }
        internal bool IsZoomed { get; set; }
        internal bool IsCloaked { get; set; }
        internal int SessionRowCount { get; set; }
        internal bool HasBackButton { get; set; }
        internal bool HasServiceAccountMarker { get; set; }
        internal bool HasNavigationStructure { get; set; }
        internal bool HasConversationStructure { get; set; }
        internal WeChatWindowRole Role { get; set; }
        internal WeChatPageKind PageKind { get; set; }
        internal int MainShellScore { get; set; }
    }

    internal sealed class SessionNavigationInfo
    {
        internal string SessionKey { get; set; }
        internal ChatSessionKind DetectedKind { get; set; }
        internal WeChatPageKind SourcePageKind { get; set; }
        internal DateTime LastSeenTime { get; set; }
    }
}
