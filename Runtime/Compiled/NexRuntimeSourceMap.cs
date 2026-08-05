using System.Collections.Generic;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// The runtime half of the source map: compiled node &lt;-&gt; the live object the backend built
    /// for it, for one screen instance.
    /// </summary>
    /// <remarks>
    /// Built during instantiation, discarded with the screen. Backend-neutral on purpose - it
    /// stores <c>object</c>, not <c>GameObject</c> - so the uGUI and UI Toolkit loaders populate
    /// the same structure and the runtime debugger has one thing to read instead of two.
    ///
    /// Together with <see cref="NexScreenProgram.SourceMap"/> this completes the chain the
    /// product promises: live object -> compiled node -> authoring element. Any of the three can
    /// be the starting point, which is what lets the debugger answer both "what authored this
    /// object?" and "what does my element look like right now?".
    /// </remarks>
    public sealed class NexRuntimeSourceMap
    {
        private readonly NexScreenProgram _program;
        private readonly object[] _instances;
        private readonly Dictionary<object, int> _indexByInstance;

        public NexScreenProgram Program => _program;

        public NexRuntimeSourceMap(NexScreenProgram program)
        {
            _program = program;
            var count = program != null && program.Nodes != null ? program.Nodes.Length : 0;
            _instances = new object[count];
            _indexByInstance = new Dictionary<object, int>(count);
        }

        public void Register(int nodeIndex, object instance)
        {
            if (instance == null) return;
            if (nodeIndex < 0 || nodeIndex >= _instances.Length) return;

            _instances[nodeIndex] = instance;
            _indexByInstance[instance] = nodeIndex;
        }

        /// <summary>The live object built for a compiled node, or null.</summary>
        public object InstanceAt(int nodeIndex)
            => nodeIndex >= 0 && nodeIndex < _instances.Length ? _instances[nodeIndex] : null;

        /// <summary>The live object built for an authoring element, or null.</summary>
        public object InstanceOfNode(string nodeId)
            => InstanceAt(_program != null ? _program.IndexOfNode(nodeId) : -1);

        /// <summary>Compiled node index that produced a live object, or -1.</summary>
        public int IndexOfInstance(object instance)
        {
            if (instance == null) return -1;
            return _indexByInstance.TryGetValue(instance, out var index) ? index : -1;
        }

        /// <summary>
        /// Authoring path for a live object - the string every runtime message should use when
        /// naming an element, since it is the only form the author recognises.
        /// </summary>
        public string AuthoringPathOf(object instance)
        {
            var index = IndexOfInstance(instance);
            if (index < 0 || _program == null) return string.Empty;

            var path = _program.SourceMap.PathOfIndex(index);
            return !string.IsNullOrEmpty(path) ? path : _program.Nodes[index].Name;
        }

        public bool TryGetNode(object instance, out NexNodeProgram node)
        {
            var index = IndexOfInstance(instance);
            if (index < 0 || _program == null)
            {
                node = default;
                return false;
            }

            node = _program.Nodes[index];
            return true;
        }

        public void Clear()
        {
            for (int i = 0; i < _instances.Length; i++) _instances[i] = null;
            _indexByInstance.Clear();
        }
    }
}
