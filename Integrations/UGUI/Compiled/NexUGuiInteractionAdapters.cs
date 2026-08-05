using emiteat.NexUI.Compiled;
using emiteat.NexUI.Interaction;
using emiteat.NexUI.State;
using TMPro;
using UnityEngine;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>Exposes a <see cref="UIStateStore"/> to the interaction engine.</summary>
    /// <remarks>
    /// The adapter exists so the engine stays free of any particular state implementation. It is
    /// five lines because that is the whole surface the engine needs - if this ever grows, the
    /// port is being asked to do too much.
    /// </remarks>
    public sealed class NexStateStoreAccess : INexStateAccess
    {
        private readonly UIStateStore _store;

        public NexStateStoreAccess(UIStateStore store) => _store = store;

        public bool TryGet(string key, out object value)
        {
            if (_store != null) return _store.TryGet(key, out value);
            value = null;
            return false;
        }

        public void Set(string key, object value) => _store?.Set(key, value);
    }

    /// <summary>Lets the interaction engine act on the built uGUI objects by compiled node index.</summary>
    /// <remarks>
    /// Resolution goes through the runtime source map, which is the same table the debugger and
    /// the flow trace read. One mapping, three consumers - so an action, a trace line and a
    /// debugger row can never disagree about which object a node became.
    /// </remarks>
    public sealed class NexUGuiScreenSurface : INexScreenSurface
    {
        private readonly NexRuntimeSourceMap _sourceMap;

        public NexUGuiScreenSurface(NexRuntimeSourceMap sourceMap) => _sourceMap = sourceMap;

        public void SetVisible(int nodeIndex, bool visible)
        {
            var go = Resolve(nodeIndex);
            if (go != null) go.SetActive(visible);
        }

        public void SetText(int nodeIndex, string text)
        {
            var go = Resolve(nodeIndex);
            if (go == null) return;

            // GetComponentInChildren so a Button's label child is reachable the same way a Label's
            // own component is - the author targeted the element, not its internal structure.
            var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = text ?? string.Empty;
        }

        private GameObject Resolve(int nodeIndex)
            => _sourceMap != null ? _sourceMap.InstanceAt(nodeIndex) as GameObject : null;
    }
}
