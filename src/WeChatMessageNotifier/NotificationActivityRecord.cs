// Privacy-preserving in-memory notification decision record.

using System;

namespace WeChatMessageNotifier
{
    internal sealed class NotificationActivityRecord
    {
        internal DateTime Timestamp { get; set; }
        internal string ContactHash { get; set; }
        internal ChatSessionKind DetectedKind { get; set; }
        internal ChatSessionKind ResolvedKind { get; set; }
        internal bool Delivered { get; set; }
        internal string Reason { get; set; }

        internal string ResultText
        {
            get { return Delivered ? "已提醒" : "已拦截"; }
        }
    }
}
