using System;
using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Compiled
{
    /// <summary>
    /// A transform nudge applied to one named internal part of a control - a slider's handle, a
    /// dropdown's caption, an input field's text area.
    /// </summary>
    /// <remarks>
    /// Identified by <see cref="PartId"/> and not by a child path, even though the authoring
    /// registry knows a path. The path it knows is Unity's stock control layout
    /// (<c>Fill Area/Fill</c>), which is what the <em>prefab</em> writer produces; the compiled
    /// builder assembles a leaner control of its own and its children sit elsewhere. Carrying the
    /// path would have compiled a <c>Find</c> that never matches. The part id is the one identity
    /// both builders can agree on, so the backend maps it to whatever it actually built.
    ///
    /// Every value is a delta from the control's own baseline, and each has its own flag: an
    /// author who nudged a handle sideways said nothing about its size, and a struct that cannot
    /// tell "unset" from "zero" would silently flatten it.
    /// </remarks>
    [Serializable]
    public struct NexPartOverride
    {
        /// <summary>Node whose control owns the part. Always valid - the compiler dropped the rest.</summary>
        public int NodeIndex;

        /// <summary>Authoring part id: <c>handle</c>, <c>fill</c>, <c>label</c>, <c>template</c>.</summary>
        public string PartId;

        public bool HasPosition;
        public Vector2 Position;

        public bool HasSizeDelta;
        public Vector2 SizeDelta;

        public bool HasRotation;
        public float Rotation;

        public bool HasScale;
        public Vector2 Scale;

        public bool HasVisibility;
        public bool Visible;
    }

    /// <summary>
    /// Every internal-part nudge on a screen, resolved against its node table.
    /// </summary>
    /// <remarks>
    /// A flat screen-level list rather than an array hanging off each node: Unity serializes nested
    /// collections poorly, and almost no node has one of these, so a per-node array would be an
    /// empty allocation on every node of every screen to serve a handful.
    /// </remarks>
    [Serializable]
    public sealed class NexPartProgram
    {
        public List<NexPartOverride> Overrides = new List<NexPartOverride>();

        public bool IsEmpty => Overrides.Count == 0;

        /// <summary>The overrides that belong to one node, in authored order.</summary>
        public IEnumerable<NexPartOverride> For(int nodeIndex)
        {
            for (int i = 0; i < Overrides.Count; i++)
                if (Overrides[i].NodeIndex == nodeIndex) yield return Overrides[i];
        }
    }
}
