using System.Collections.Generic;
using UnityEngine.UI;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Rewires explicit uGUI <see cref="Navigation"/> chains for dynamically-populated content
    /// (inventory grids, dynamic lists) where Unity's default Automatic navigation mode gets
    /// confused - Automatic re-scans proximity every layout pass and often produces skipped or
    /// looping chains once items are pooled/recycled (see <see cref="Components.UIItemPool{TView}"/>).
    /// Call <see cref="WireVertical"/>/<see cref="WireGrid"/> once after a list/grid's items are
    /// (re)populated, in declaration/visual order.
    /// </summary>
    public static class UGUIDynamicNavigation
    {
        /// <summary>Wires a single-column vertical chain: Up/Down move between consecutive items; Left/Right are untouched.</summary>
        public static void WireVertical(IReadOnlyList<Selectable> items, bool wrap = false)
        {
            if (items == null || items.Count == 0) return;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;

                var nav = item.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = ResolveVertical(items, i - 1, wrap);
                nav.selectOnDown = ResolveVertical(items, i + 1, wrap);
                item.navigation = nav;
            }
        }

        /// <summary>Wires a row-major grid chain with the given column count: Up/Down/Left/Right move between adjacent cells.</summary>
        public static void WireGrid(IReadOnlyList<Selectable> items, int columnCount, bool wrap = false)
        {
            if (items == null || items.Count == 0 || columnCount <= 0) return;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;

                var row = i / columnCount;
                var col = i % columnCount;
                var rowStart = row * columnCount;
                var rowCount = System.Math.Min(columnCount, items.Count - rowStart);

                var nav = item.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnLeft = ResolveInRow(items, rowStart, rowCount, col - 1, wrap);
                nav.selectOnRight = ResolveInRow(items, rowStart, rowCount, col + 1, wrap);
                nav.selectOnUp = ResolveVertical(items, i - columnCount, wrap);
                nav.selectOnDown = ResolveVertical(items, i + columnCount, wrap);
                item.navigation = nav;
            }
        }

        private static Selectable ResolveVertical(IReadOnlyList<Selectable> items, int index, bool wrap)
        {
            if (index >= 0 && index < items.Count) return items[index];
            if (!wrap || items.Count == 0) return null;
            return items[((index % items.Count) + items.Count) % items.Count];
        }

        private static Selectable ResolveInRow(IReadOnlyList<Selectable> items, int rowStart, int rowCount, int col, bool wrap)
        {
            if (rowCount <= 0) return null;
            if (col >= 0 && col < rowCount) return items[rowStart + col];
            if (!wrap) return null;
            return items[rowStart + ((col % rowCount) + rowCount) % rowCount];
        }
    }
}
