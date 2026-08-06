using System.Collections.Generic;
using emiteat.NexUI.Vector;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Draws an arbitrary vector path in uGUI - polygons, stars, rings, arcs, or anything a pen
    /// tool produced.
    /// </summary>
    /// <remarks>
    /// uGUI can draw a rect with a sprite and nothing else. Every non-rectangular shape in a Unity
    /// UI is therefore an imported PNG, which means it cannot be recoloured without a second asset,
    /// goes soft when scaled, and occupies an atlas slot. This component removes that constraint:
    /// the shape is geometry, so it is crisp at any size and costs no texture memory.
    ///
    /// Tessellation is cached and only redone when the path, the size or the tessellation quality
    /// changes. A star with fifty points is a few hundred triangles to generate, which is nothing
    /// once but real if it happened on every layout pass - and uGUI rebuilds a canvas far more
    /// often than authors expect.
    /// </remarks>
    [AddComponentMenu("NexUI/Graphics/NX Vector Graphic")]
    public sealed class NXVectorGraphic : MaskableGraphic
    {
        [SerializeField, Tooltip("Curve flattening quality. Lower is smoother and costs more triangles.")]
        private float m_CordDeviation = 0.25f;

        private NexVectorShape _shape;
        private List<NexVectorTessellator.Mesh2D> _cached;
        private Rect _cachedRect;
        private float _cachedDeviation;

        /// <summary>
        /// The path this draws. Assigning re-tessellates; mutating in place does not.
        /// </summary>
        /// <remarks>
        /// A setter rather than a mutable property because the cache has to know. Callers editing
        /// the returned shape in place call <see cref="Refresh"/> - which is also what a pen tool
        /// does on every drag, so it is the common path rather than an afterthought.
        /// </remarks>
        public NexVectorShape Shape
        {
            get => _shape;
            set
            {
                _shape = value;
                Refresh();
            }
        }

        /// <summary>Re-tessellates after the shape was changed in place.</summary>
        public void Refresh()
        {
            _cached = null;
            SetVerticesDirty();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            // The path is authored in the element's own space, so a resize changes where it lands.
            _cached = null;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (_shape == null || _shape.IsEmpty) return;

            var rect = GetPixelAdjustedRect();
            EnsureTessellated(rect);
            if (_cached == null || _cached.Count == 0) return;

            // The path is authored in a normalised box; map it onto the current rect so the same
            // shape fits whatever the layout gave this element.
            var bounds = _shape.Bounds();
            var scaleX = bounds.width > 0f ? rect.width / bounds.width : 1f;
            var scaleY = bounds.height > 0f ? rect.height / bounds.height : 1f;

            // Paths are stored y-down - the convention SVG uses and the one the Designer canvas
            // authors in - while uGUI's local space is y-up. Flipping here rather than at each
            // producer is what lets an imported SVG and a pen-drawn path share one model: the
            // alternative is every caller remembering to mirror, and one that forgets renders
            // upside down with nothing to point at.

            for (var m = 0; m < _cached.Count; m++)
            {
                var mesh = _cached[m];
                if (mesh.IsEmpty) continue;

                var start = vertexHelper.currentVertCount;
                var tint = mesh.Color * color;

                for (var v = 0; v < mesh.Vertices.Length; v++)
                {
                    var point = mesh.Vertices[v];
                    vertexHelper.AddVert(new Vector3(
                            rect.xMin + (point.x - bounds.xMin) * scaleX,
                            rect.yMax - (point.y - bounds.yMin) * scaleY),
                        tint, Vector2.zero);
                }

                for (var i = 0; i + 2 < mesh.Indices.Length; i += 3)
                {
                    vertexHelper.AddTriangle(
                        start + mesh.Indices[i],
                        start + mesh.Indices[i + 1],
                        start + mesh.Indices[i + 2]);
                }
            }
        }

        private void EnsureTessellated(Rect rect)
        {
            if (_cached != null && _cachedRect == rect && Mathf.Approximately(_cachedDeviation, m_CordDeviation))
                return;

            var options = NexVectorTessellator.DefaultOptions;
            options.MaxCordDeviation = Mathf.Max(0.01f, m_CordDeviation);

            _cached = NexVectorTessellator.Tessellate(_shape, options);
            _cachedRect = rect;
            _cachedDeviation = m_CordDeviation;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            Refresh();
        }
#endif
    }
}
