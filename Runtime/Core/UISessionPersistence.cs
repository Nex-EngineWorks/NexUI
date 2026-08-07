using System.Threading.Tasks;

namespace emiteat.NexUI.Core
{
    /// <summary>Pluggable key/value store for <see cref="UISessionPersistence"/> so Core doesn't hard-couple to <see cref="UnityEngine.PlayerPrefs"/>.</summary>
    public interface IUISessionStore
    {
        void SetString(string key, string value);
        string GetString(string key, string defaultValue = null);
    }

    /// <summary>Default store backed by <see cref="UnityEngine.PlayerPrefs"/>.</summary>
    public sealed class PlayerPrefsSessionStore : IUISessionStore
    {
        public void SetString(string key, string value) => UnityEngine.PlayerPrefs.SetString(key, value);
        public string GetString(string key, string defaultValue = null) => UnityEngine.PlayerPrefs.GetString(key, defaultValue);
    }

    /// <summary>
    /// B7: opt-in last-open-screen persistence. Tracks screens on one layer (Window by default -
    /// the layer menus/settings typically live on) across <see cref="UIManager.ScreenOpened"/>/
    /// <see cref="UIManager.ScreenClosed"/> and restores whichever was open when the session
    /// ended. Construct once after registering screens/factories; nothing happens until you do -
    /// existing <see cref="UIManager"/> behavior is unchanged if you never create this.
    /// </summary>
    public sealed class UISessionPersistence
    {
        private const string LastScreenKey = "nexui.session.lastScreen";

        private readonly UIManager _manager;
        private readonly IUISessionStore _store;
        private readonly UILayerType _trackedLayer;

        public UISessionPersistence(UIManager manager, IUISessionStore store = null, UILayerType trackedLayer = UILayerType.Window)
        {
            _manager = manager;
            _store = store ?? new PlayerPrefsSessionStore();
            _trackedLayer = trackedLayer;
            _manager.ScreenOpened += OnScreenOpened;
            _manager.ScreenClosed += OnScreenClosed;
        }

        /// <summary>Opens whatever screen was last recorded, if any. Call once at startup after registering screens/factories.</summary>
        public Task RestoreAsync()
        {
            var lastScreen = _store.GetString(LastScreenKey);
            return string.IsNullOrEmpty(lastScreen) ? Task.CompletedTask : _manager.OpenAsync(lastScreen);
        }

        private void OnScreenOpened(UIScreenInstance instance)
        {
            if (instance.Layer != _trackedLayer) return;
            _store.SetString(LastScreenKey, instance.ScreenId);
        }

        private void OnScreenClosed(UIScreenInstance instance)
        {
            if (instance.Layer != _trackedLayer) return;
            // Only clear the record if it still points at this screen - avoids clobbering a
            // newer screen's record if closes/opens race (e.g. a ReplaceLayer switch).
            if (_store.GetString(LastScreenKey) == instance.ScreenId)
                _store.SetString(LastScreenKey, string.Empty);
        }
    }
}
