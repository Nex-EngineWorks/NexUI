using System;
using System.Threading.Tasks;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// B7: resolves a <see cref="UIDeepLink"/> by opening its target screen through the normal
    /// <see cref="UIManager.OpenAsync"/> path, then handing each param off to <see cref="OnParam"/>
    /// for the caller to apply however its project models nested state (a UIStateStore key, a
    /// screen-specific controller call, ...). Core intentionally has no opinion on what a "tab"
    /// is - that decoupling mirrors <see cref="IUISessionStore"/> in <see cref="UISessionPersistence"/>.
    /// Useful for QA (jump straight to "Settings/Audio") and push-notification-driven entry points.
    /// </summary>
    public sealed class UIDeepLinkRouter
    {
        private readonly UIManager _manager;

        /// <summary>Invoked once per query param after the screen opens: (screenId, paramKey, paramValue).</summary>
        public event Action<string, string, string> OnParam;

        public UIDeepLinkRouter(UIManager manager) => _manager = manager;

        public async Task NavigateAsync(string link, UIOpenArgs args = default)
        {
            var deepLink = UIDeepLink.Parse(link);
            if (string.IsNullOrEmpty(deepLink.ScreenId)) return;

            await _manager.OpenAsync(deepLink.ScreenId, args);

            foreach (var pair in deepLink.Params)
                OnParam?.Invoke(deepLink.ScreenId, pair.Key, pair.Value);
        }
    }
}
