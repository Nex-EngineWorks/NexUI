using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Components;
using UnityEngine;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>uGUI implementation of <see cref="INXProgressBar"/> (backed by a Slider).</summary>
    public sealed class NXProgressBarUGUI : INXProgressBar
    {
        private readonly IUIValueCapability _value;

        public IUIElementHandle Handle { get; }

        public NXProgressBarUGUI(IUIElementHandle handle)
        {
            Handle = handle;
            _value = handle?.As<IUIValueCapability>();
            if (_value == null)
                Debug.LogWarning($"[NexUI] NXProgressBarUGUI: '{handle?.Id}' has no value capability.");
        }

        public float Value
        {
            get => _value?.Value ?? 0f;
            set { if (_value != null) _value.Value = value; }
        }

        public float Min
        {
            get => _value?.Min ?? 0f;
            set { if (_value != null) _value.Min = value; }
        }

        public float Max
        {
            get => _value?.Max ?? 1f;
            set { if (_value != null) _value.Max = value; }
        }

        public float Normalized
        {
            get
            {
                if (_value == null) return 0f;
                float range = _value.Max - _value.Min;
                return range <= 0f ? 0f : Mathf.Clamp01((_value.Value - _value.Min) / range);
            }
        }
    }
}
