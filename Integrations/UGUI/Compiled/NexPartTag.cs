using UnityEngine;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Marks an object the compiled builder created as one of its control's named parts.
    /// </summary>
    /// <remarks>
    /// The authoring registry identifies parts by id and also knows a child path, but that path
    /// describes Unity's stock controls, which is what the <em>prefab</em> writer produces. The
    /// compiled builder assembles a leaner control of its own, so a path lookup compiled from the
    /// registry would never match. A tag lets each builder name what it actually built, and leaves
    /// the part id as the single identity both agree on.
    ///
    /// A component rather than a naming convention: names are user-visible in the hierarchy and get
    /// changed, and a screen whose handle stopped moving because someone renamed an object would be
    /// very hard to explain.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NexPartTag : MonoBehaviour
    {
        [SerializeField] private string _partId;

        /// <summary>Authoring part id: <c>handle</c>, <c>fill</c>, <c>label</c>, <c>template</c>.</summary>
        public string PartId => _partId;

        /// <summary>Tags a freshly built child. Only the builder calls this.</summary>
        public static void Mark(Component target, string partId)
        {
            if (target == null || string.IsNullOrEmpty(partId)) return;

            var tag = target.gameObject.GetComponent<NexPartTag>();
            if (tag == null) tag = target.gameObject.AddComponent<NexPartTag>();
            tag._partId = partId;
        }

        /// <summary>
        /// The tagged descendant of <paramref name="root"/> with this part id, or null.
        /// </summary>
        /// <remarks>
        /// Searches descendants but stops at nothing: a control's parts are always inside it, and
        /// the alternative - remembering every tag in a dictionary at build time - would cost an
        /// allocation on every screen to speed up a lookup that happens once per authored nudge.
        /// </remarks>
        public static RectTransform Find(Transform root, string partId)
        {
            if (root == null || string.IsNullOrEmpty(partId)) return null;

            var tags = root.GetComponentsInChildren<NexPartTag>(includeInactive: true);
            for (int i = 0; i < tags.Length; i++)
                if (string.Equals(tags[i]._partId, partId, System.StringComparison.Ordinal))
                    return tags[i].transform as RectTransform;

            return null;
        }
    }
}
