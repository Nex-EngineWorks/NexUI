using System.Collections;
using UnityEngine;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.State;

namespace emiteat.NexUI.Samples.BasicRuntime
{
    /// <summary>
    /// Demonstrates the state store + capability-based binders against the HUD surface, and
    /// shows a toast. Bind element ids: "nameLabel" (text) and "hpBar" (value).
    /// For uGUI, tag those GameObjects with NxUGuiBindingTag; for UI Toolkit, name them so.
    /// </summary>
    public sealed class BasicRuntimeStateDemo : MonoBehaviour
    {
        [SerializeField] private string _hudScreenId = "HUD";
        [SerializeField] private string _nameElementId = "nameLabel";
        [SerializeField] private string _hpElementId = "hpBar";

        private readonly UIStateStore _store = new UIStateStore();
        private UITextBinder _nameBinder;
        private UIValueBinder _hpBinder;

        private IEnumerator Start()
        {
            _store.Set("player.name", "Hero");
            _store.Set("player.hp", 1f);

            // Wait until the HUD is open and its surface is available.
            IUISurface surface = null;
            while (surface == null)
            {
                surface = Core.NexUI.Manager.GetSurface(_hudScreenId);
                if (surface == null) yield return null;
            }

            var nameHandle = surface.TryFind(_nameElementId);
            if (nameHandle != null)
            {
                _nameBinder = new UITextBinder();
                _nameBinder.Bind(nameHandle, "player.name", _store);
            }

            var hpHandle = surface.TryFind(_hpElementId);
            if (hpHandle != null)
            {
                _hpBinder = new UIValueBinder();
                _hpBinder.Bind(hpHandle, "player.hp", _store);
            }

            // Animate HP down over a few seconds to prove the binding is live.
            float hp = 1f;
            while (hp > 0f)
            {
                hp -= Time.deltaTime * 0.1f;
                _store.Set("player.hp", Mathf.Max(0f, hp));
                yield return null;
            }

            _store.Set("player.name", "Hero (down!)");
        }

        private void OnDestroy()
        {
            _nameBinder?.Unbind();
            _hpBinder?.Unbind();
        }
    }
}
