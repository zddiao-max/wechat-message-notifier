using System;
using System.Text.RegularExpressions;

namespace WeChatMessageNotifier
{
    // Only stable UIA identities and their routing metadata cross the toast
    // activation boundary. Display names and message bodies are never stored.
    internal enum ToastActivationRoute
    {
        MainSession,
        ServiceAccount
    }

    internal sealed class ToastActivationRequest
    {
        internal ToastActivationRequest(
            string sessionKey,
            ChatSessionKind sessionKind,
            ToastActivationRoute route)
        {
            SessionKey = sessionKey;
            SessionKind = sessionKind;
            Route = route;
        }

        internal string SessionKey { get; private set; }
        internal ChatSessionKind SessionKind { get; private set; }
        internal ToastActivationRoute Route { get; private set; }

        internal static ToastActivationRequest FromSession(
            string sessionKey,
            ChatSessionKind kind)
        {
            return new ToastActivationRequest(
                sessionKey,
                kind,
                kind == ChatSessionKind.ServiceAccount
                    ? ToastActivationRoute.ServiceAccount
                    : ToastActivationRoute.MainSession);
        }

        internal string ToJson()
        {
            return "{\"session\":\"" + Escape(SessionKey) +
                   "\",\"kind\":\"" + SessionKind +
                   "\",\"route\":\"" + Route + "\"}";
        }

        internal static bool TryParseJson(
            string value,
            out ToastActivationRequest request)
        {
            request = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var session = Regex.Match(
                value,
                "\\\"session\\\"\\s*:\\s*\\\"(?<value>(?:\\\\.|[^\\\"])*)\\\"",
                RegexOptions.CultureInvariant);
            if (!session.Success)
            {
                return false;
            }

            ChatSessionKind kind = ChatSessionKind.Unknown;
            ToastActivationRoute route = ToastActivationRoute.MainSession;
            var kindMatch = Regex.Match(
                value,
                "\\\"kind\\\"\\s*:\\s*\\\"(?<value>[A-Za-z]+)\\\"",
                RegexOptions.CultureInvariant);
            if (kindMatch.Success &&
                (!Enum.TryParse(kindMatch.Groups["value"].Value, true, out kind) ||
                 !Enum.IsDefined(typeof(ChatSessionKind), kind)))
            {
                return false;
            }

            var routeMatch = Regex.Match(
                value,
                "\\\"route\\\"\\s*:\\s*\\\"(?<value>[A-Za-z]+)\\\"",
                RegexOptions.CultureInvariant);
            if (routeMatch.Success &&
                (!Enum.TryParse(routeMatch.Groups["value"].Value, true, out route) ||
                 !Enum.IsDefined(typeof(ToastActivationRoute), route)))
            {
                return false;
            }

            request = new ToastActivationRequest(
                Unescape(session.Groups["value"].Value), kind, route);
            return !string.IsNullOrWhiteSpace(request.SessionKey);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string Unescape(string value)
        {
            return (value ?? string.Empty).Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
