using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Integrations.UIToolkit
{
    /// <summary>
    /// Gradient fill for UI Toolkit. USS can do solid colours and border radius but has no gradient,
    /// so this paints one with Painter2D and honours the element's own border radius.
    /// </summary>
    [UxmlElement]
    public partial class NXGradientElement : VisualElement
    {
        [UxmlAttribute] public Color startColor { get; set; } = Color.white;
        [UxmlAttribute] public Color endColor { get; set; } = new Color(0.6f, 0.6f, 0.6f, 1f);
        [UxmlAttribute, Tooltip("Gradient direction in degrees. 90 is bottom to top.")]
        public float angle { get; set; } = 90f;
        [UxmlAttribute, Tooltip("Bands used to approximate the ramp. Higher is smoother.")]
        public int steps { get; set; } = 24;

        public NXGradientElement()
        {
            generateVisualContent += Paint;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        private void Paint(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            var painter = context.painter2D;
            var count = Mathf.Max(2, steps);
            var radians = angle * Mathf.Deg2Rad;
            var horizontal = Mathf.Abs(Mathf.Cos(radians)) >= Mathf.Abs(Mathf.Sin(radians));

            // Painter2D has no gradient brush, so the ramp is drawn as adjacent bands along the axis
            // closest to the requested angle. Bands overlap by a pixel to avoid seams.
            for (var i = 0; i < count; i++)
            {
                var t0 = i / (float)count;
                var t1 = (i + 1) / (float)count;
                painter.fillColor = Color.Lerp(startColor, endColor, (t0 + t1) * 0.5f);
                painter.BeginPath();

                if (horizontal)
                {
                    var x0 = Mathf.Lerp(rect.xMin, rect.xMax, Mathf.Cos(radians) >= 0f ? t0 : 1f - t1);
                    var x1 = Mathf.Lerp(rect.xMin, rect.xMax, Mathf.Cos(radians) >= 0f ? t1 : 1f - t0);
                    painter.MoveTo(new Vector2(x0, rect.yMin));
                    painter.LineTo(new Vector2(x1 + 1f, rect.yMin));
                    painter.LineTo(new Vector2(x1 + 1f, rect.yMax));
                    painter.LineTo(new Vector2(x0, rect.yMax));
                }
                else
                {
                    var y0 = Mathf.Lerp(rect.yMax, rect.yMin, Mathf.Sin(radians) >= 0f ? t0 : 1f - t1);
                    var y1 = Mathf.Lerp(rect.yMax, rect.yMin, Mathf.Sin(radians) >= 0f ? t1 : 1f - t0);
                    painter.MoveTo(new Vector2(rect.xMin, y0));
                    painter.LineTo(new Vector2(rect.xMax, y0));
                    painter.LineTo(new Vector2(rect.xMax, y1 - 1f));
                    painter.LineTo(new Vector2(rect.xMin, y1 - 1f));
                }

                painter.ClosePath();
                painter.Fill();
            }
        }
    }

    /// <summary>
    /// Applies the device safe area as padding. UI Toolkit exposes no safe-area handling, so every
    /// mobile project writes this against <see cref="Screen.safeArea"/> by hand.
    /// </summary>
    [UxmlElement]
    public partial class NXSafeAreaElement : VisualElement
    {
        [UxmlAttribute] public bool applyLeft { get; set; } = true;
        [UxmlAttribute] public bool applyRight { get; set; } = true;
        [UxmlAttribute] public bool applyTop { get; set; } = true;
        [UxmlAttribute] public bool applyBottom { get; set; } = true;

        private Rect _applied;

        public NXSafeAreaElement()
        {
            style.flexGrow = 1f;
            // Safe area changes on rotation without raising an event, so the check rides along with
            // the panel's own update instead of polling on a timer.
            schedule.Execute(Apply).Every(250);
            RegisterCallback<AttachToPanelEvent>(_ => Apply());
        }

        public void Apply()
        {
            var safeArea = Screen.safeArea;
            if (safeArea == _applied) return;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            _applied = safeArea;

            var left = applyLeft ? safeArea.xMin : 0f;
            var right = applyRight ? Screen.width - safeArea.xMax : 0f;
            var top = applyTop ? Screen.height - safeArea.yMax : 0f;
            var bottom = applyBottom ? safeArea.yMin : 0f;

            style.paddingLeft = left;
            style.paddingRight = right;
            style.paddingTop = top;
            style.paddingBottom = bottom;
        }
    }

    /// <summary>
    /// Arranges children around a circle or arc - radial menus and ability wheels. Flexbox cannot
    /// express this, so it is done by positioning children absolutely on layout.
    /// </summary>
    [UxmlElement]
    public partial class NXRadialContainer : VisualElement
    {
        [UxmlAttribute] public float radius { get; set; } = 120f;
        [UxmlAttribute] public float startAngle { get; set; } = 90f;
        [UxmlAttribute, Tooltip("Sweep covered by all children. 360 spreads them evenly around.")]
        public float sweepAngle { get; set; } = 360f;

        public NXRadialContainer()
        {
            RegisterCallback<GeometryChangedEvent>(_ => Arrange());
        }

        public void Arrange()
        {
            var count = childCount;
            if (count == 0) return;

            var full = Mathf.Abs(Mathf.Abs(sweepAngle) - 360f) < 0.01f;
            var divisor = full ? count : Mathf.Max(1, count - 1);
            var centre = new Vector2(contentRect.width * 0.5f, contentRect.height * 0.5f);

            for (var i = 0; i < count; i++)
            {
                var child = this[i];
                var degrees = startAngle + sweepAngle * (i / (float)divisor);
                var radians = degrees * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians)) * radius;

                child.style.position = Position.Absolute;
                child.style.left = centre.x + offset.x - child.resolvedStyle.width * 0.5f;
                child.style.top = centre.y + offset.y - child.resolvedStyle.height * 0.5f;
            }
        }
    }

    /// <summary>Bar split into discrete segments (health pips, ammo, shield chunks).</summary>
    [UxmlElement]
    public partial class NXSegmentedBarElement : VisualElement
    {
        [UxmlAttribute] public int segments { get; set; } = 5;
        [UxmlAttribute] public float value { get; set; } = 1f;
        [UxmlAttribute] public float gap { get; set; } = 4f;
        [UxmlAttribute] public Color fillColor { get; set; } = new Color(0.25f, 0.55f, 0.95f);
        [UxmlAttribute] public Color emptyColor { get; set; } = new Color(1f, 1f, 1f, 0.15f);
        [UxmlAttribute] public bool vertical { get; set; }

        public NXSegmentedBarElement()
        {
            generateVisualContent += Paint;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        public void SetValue(float normalized)
        {
            value = Mathf.Clamp01(normalized);
            MarkDirtyRepaint();
        }

        private void Paint(MeshGenerationContext context)
        {
            var rect = contentRect;
            var count = Mathf.Max(1, segments);
            var total = vertical ? rect.height : rect.width;
            var size = (total - gap * (count - 1)) / count;
            if (size <= 0f) return;

            var painter = context.painter2D;
            var filled = Mathf.Clamp01(value) * count;

            for (var i = 0; i < count; i++)
            {
                var offset = i * (size + gap);
                var slot = vertical
                    ? new Rect(rect.xMin, rect.yMax - offset - size, rect.width, size)
                    : new Rect(rect.xMin + offset, rect.yMin, size, rect.height);

                Quad(painter, slot, emptyColor);

                var fill = Mathf.Clamp01(filled - i);
                if (fill <= 0f) continue;
                var full = vertical
                    ? new Rect(slot.xMin, slot.yMax - slot.height * fill, slot.width, slot.height * fill)
                    : new Rect(slot.xMin, slot.yMin, slot.width * fill, slot.height);
                Quad(painter, full, fillColor);
            }
        }

        private static void Quad(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }
    }

    /// <summary>Radial cooldown sweep over an ability icon.</summary>
    [UxmlElement]
    public partial class NXCooldownElement : VisualElement
    {
        [UxmlAttribute, Tooltip("1 is fully covered (just triggered), 0 is ready.")]
        public float remaining { get; set; }
        [UxmlAttribute] public Color overlayColor { get; set; } = new Color(0f, 0f, 0f, 0.55f);
        [UxmlAttribute] public bool clockwise { get; set; } = true;

        public NXCooldownElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        public void SetRemaining(float normalized)
        {
            remaining = Mathf.Clamp01(normalized);
            MarkDirtyRepaint();
        }

        private void Paint(MeshGenerationContext context)
        {
            if (remaining <= 0f) return;
            var rect = contentRect;
            var centre = rect.center;
            var radius = new Vector2(rect.width, rect.height).magnitude * 0.5f;

            var painter = context.painter2D;
            painter.fillColor = overlayColor;
            painter.BeginPath();
            painter.MoveTo(centre);
            var sweep = 360f * Mathf.Clamp01(remaining);
            painter.Arc(centre, radius, 90f, clockwise ? 90f - sweep : 90f + sweep,
                clockwise ? ArcDirection.Clockwise : ArcDirection.CounterClockwise);
            painter.ClosePath();
            painter.Fill();
        }
    }
}
