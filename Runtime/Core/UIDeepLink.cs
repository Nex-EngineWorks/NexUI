using System.Collections.Generic;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// B7: parses a deep-link string of the form <c>"ScreenId"</c> or
    /// <c>"ScreenId?key=value&amp;key2=value2"</c> into a screen id plus a flat param map (e.g.
    /// <c>"Settings?tab=Audio"</c> for "jump to Settings &gt; Audio tab"). Pure data - carries no
    /// opinion about what a param means, so it works whether the target project drives tabs via
    /// UIStateStore keys, a custom controller, or something else entirely.
    /// </summary>
    public readonly struct UIDeepLink
    {
        public readonly string ScreenId;
        public readonly IReadOnlyDictionary<string, string> Params;

        private UIDeepLink(string screenId, IReadOnlyDictionary<string, string> parameters)
        {
            ScreenId = screenId;
            Params = parameters;
        }

        public static UIDeepLink Parse(string link)
        {
            if (string.IsNullOrEmpty(link))
                return new UIDeepLink(string.Empty, EmptyParams);

            var queryStart = link.IndexOf('?');
            if (queryStart < 0)
                return new UIDeepLink(link, EmptyParams);

            var screenId = link.Substring(0, queryStart);
            var query = link.Substring(queryStart + 1);
            var parameters = new Dictionary<string, string>();

            foreach (var pair in query.Split('&'))
            {
                if (string.IsNullOrEmpty(pair)) continue;
                var eq = pair.IndexOf('=');
                if (eq < 0) { parameters[pair] = string.Empty; continue; }
                parameters[pair.Substring(0, eq)] = pair.Substring(eq + 1);
            }

            return new UIDeepLink(screenId, parameters);
        }

        private static readonly Dictionary<string, string> EmptyParams = new Dictionary<string, string>();
    }
}
