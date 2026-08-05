using System.Collections.Generic;
using emiteat.NexUI.Accessibility;
using emiteat.NexUI.Compiled;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// The accessibility semantics of one built node, carried onto the GameObject.
    /// </summary>
    /// <remarks>
    /// uGUI has no notion of a semantic role or an accessible name - a Button is a Button and a
    /// screen reader is on its own. Putting the compiled answer on the object is what lets three
    /// separate consumers agree: an assistive-technology bridge, the accessibility audit in the
    /// Studio, and an automated test asking for "the button named Purchase".
    ///
    /// Deliberately data, not behaviour. Nothing here talks to a platform screen reader, because
    /// the platform API differs per Unity version and per target, and a component that silently
    /// does nothing on half of them would be worse than one that plainly holds the data and lets a
    /// bridge read it.
    /// </remarks>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class NexAccessibleNode : MonoBehaviour
    {
        [SerializeField] private AccessibilityRole m_Role;
        [SerializeField] private string m_Label;
        [SerializeField] private int m_FocusOrder = -1;
        [SerializeField] private string m_AuthoringNodeId;

        /// <summary>Semantic role, as authored.</summary>
        public AccessibilityRole Role => m_Role;

        /// <summary>What assistive technology should announce. Never null.</summary>
        public string Label => m_Label ?? string.Empty;

        /// <summary>Position in the screen's reading order, or -1 when it is skipped.</summary>
        public int FocusOrder => m_FocusOrder;

        /// <summary>Authoring node this came from - the key the source map joins on.</summary>
        public string AuthoringNodeId => m_AuthoringNodeId ?? string.Empty;

        public bool IsFocusable => m_FocusOrder >= 0;

        internal void Apply(in NexNodeProgram node)
        {
            m_Role = node.Role;
            m_Label = node.AccessibleName;
            m_FocusOrder = node.FocusOrder;
            m_AuthoringNodeId = node.NodeId;
        }
    }

    /// <summary>Reads a built screen's accessibility semantics back out.</summary>
    public static class NexAccessibility
    {
        /// <summary>
        /// The screen's focusable nodes, in reading order.
        /// </summary>
        /// <remarks>
        /// Sorted by the compiled order rather than by hierarchy position at runtime, so a node
        /// that motion moved across the screen is still announced where the author put it.
        /// </remarks>
        public static List<NexAccessibleNode> ReadingOrder(GameObject screenRoot)
        {
            var result = new List<NexAccessibleNode>();
            if (screenRoot == null) return result;

            screenRoot.GetComponentsInChildren(includeInactive: true, result);
            result.RemoveAll(node => !node.IsFocusable);
            result.Sort((a, b) => a.FocusOrder.CompareTo(b.FocusOrder));
            return result;
        }

        /// <summary>
        /// Chains uGUI keyboard/gamepad navigation along the reading order.
        /// </summary>
        /// <remarks>
        /// Unity's automatic navigation picks the nearest selectable geometrically, which is a
        /// reasonable default and a poor answer for a form: it will happily jump from a field to
        /// the button beside it instead of to the next field. Explicit chaining makes Tab follow
        /// the order the author arranged, which is also the order the screen is read in.
        ///
        /// Wrapping is deliberate - the last element leads back to the first, so keyboard focus
        /// cannot fall out of a modal and end up somewhere invisible behind it.
        /// </remarks>
        public static void ApplyExplicitNavigation(GameObject screenRoot)
        {
            var ordered = ReadingOrder(screenRoot);

            var selectables = new List<Selectable>(ordered.Count);
            for (var i = 0; i < ordered.Count; i++)
            {
                var selectable = ordered[i].GetComponent<Selectable>();
                if (selectable != null) selectables.Add(selectable);
            }

            if (selectables.Count < 2) return;

            for (var i = 0; i < selectables.Count; i++)
            {
                var previous = selectables[(i - 1 + selectables.Count) % selectables.Count];
                var next = selectables[(i + 1) % selectables.Count];

                var navigation = selectables[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = previous;
                navigation.selectOnLeft = previous;
                navigation.selectOnDown = next;
                navigation.selectOnRight = next;
                selectables[i].navigation = navigation;
            }
        }
    }
}
