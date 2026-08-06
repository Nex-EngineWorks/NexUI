using emiteat.NexUI.Vector;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Puts a vector path onto a uGUI GameObject, for whichever writer is producing it.
    /// </summary>
    /// <remarks>
    /// Shared by the compiled-screen builder and the Designer's prefab serializer on purpose. The
    /// two used to disagree - the compiled path drew shapes and the prefab path silently dropped
    /// them - which is the same class of bug this repository keeps producing whenever an authored
    /// field is taught to one writer and not the other. One applier means "what does a shape do to
    /// a GameObject" has exactly one answer.
    /// </remarks>
    public static class NexUGuiShapeApplier
    {
        /// <summary>
        /// Draws <paramref name="shape"/> on <paramref name="target"/>, or removes any previous
        /// path when there is nothing to draw.
        /// </summary>
        /// <remarks>
        /// The rect's <see cref="Image"/> is removed rather than left underneath: a shape drawn on
        /// top of a full-rect fill is a shape sitting on a coloured box, which is never what
        /// drawing a shape meant. The image's colour is carried into an untinted shape so the
        /// authored colour survives the swap.
        /// </remarks>
        /// <returns>The graphic now drawing the path, or null if the path was removed.</returns>
        public static NXVectorGraphic Apply(GameObject target, NexVectorShape shape)
        {
            if (target == null) return null;

            if (shape == null || shape.IsEmpty)
            {
                Remove(target);
                return null;
            }

            var image = target.GetComponent<Image>();
            if (image != null)
            {
                if (shape.Filled && shape.FillColor == Color.white) shape.FillColor = image.color;
                Destroy(image);
            }

            // Reused rather than re-added, so saving the same screen twice does not stack graphics
            // and does not churn the prefab's component list on every save.
            var graphic = target.GetComponent<NXVectorGraphic>();
            if (graphic == null) graphic = target.AddComponent<NXVectorGraphic>();

            graphic.Shape = shape;
            return graphic;
        }

        /// <summary>
        /// Takes a path back off a GameObject that no longer has one.
        /// </summary>
        /// <remarks>
        /// Needed because saving is repeatable: an element that was drawn on and then had its path
        /// deleted has to lose the graphic too, or the prefab keeps rendering a shape the document
        /// no longer describes.
        /// </remarks>
        public static bool Remove(GameObject target)
        {
            if (target == null) return false;

            var graphic = target.GetComponent<NXVectorGraphic>();
            if (graphic == null) return false;

            Destroy(graphic);
            return true;
        }

        private static void Destroy(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
