using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.State
{
    public enum UIBindingMode
    {
        OneWay,
        TwoWay,
        OneWayToSource
    }

    /// <summary>Non-generic converter contract used by metadata/key-based bindings.</summary>
    public interface IUIBindingConverter
    {
        object Convert(object source);
        object ConvertBack(object target);
    }

    /// <summary>Project-owned converter lookup. Keys are stored in Designer binding metadata.</summary>
    public sealed class UIBindingConverterRegistry
    {
        private readonly Dictionary<string, IUIBindingConverter> _converters =
            new Dictionary<string, IUIBindingConverter>(StringComparer.Ordinal);

        public void Register(string key, IUIBindingConverter converter)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Converter key is required.", nameof(key));
            _converters[key] = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        public bool Remove(string key) => !string.IsNullOrEmpty(key) && _converters.Remove(key);

        public bool TryResolve(string key, out IUIBindingConverter converter)
        {
            if (string.IsNullOrEmpty(key)) { converter = null; return false; }
            return _converters.TryGetValue(key, out converter);
        }
    }

    public sealed class DelegateBindingConverter : IUIBindingConverter
    {
        private readonly Func<object, object> _convert;
        private readonly Func<object, object> _convertBack;

        public DelegateBindingConverter(Func<object, object> convert, Func<object, object> convertBack = null)
        {
            _convert = convert ?? throw new ArgumentNullException(nameof(convert));
            _convertBack = convertBack;
        }

        public object Convert(object source) => _convert(source);

        public object ConvertBack(object target)
            => _convertBack != null ? _convertBack(target) : throw new NotSupportedException("Converter is one-way.");
    }

    /// <summary>
    /// Base for one-way / two-way bindings between a state key and an element
    /// capability. Binders resolve capabilities via <see cref="IUIElementHandle.As{T}"/>
    /// and never touch a concrete backend type.
    /// </summary>
    public abstract class UIBinder
    {
        public abstract void Bind(IUIElementHandle target, string key, UIStateStore store);
        public abstract void Unbind();

        /// <summary>
        /// Helper: resolve a required capability, logging a warning (not silently failing)
        /// when the element does not provide it.
        /// </summary>
        protected static TCapability Require<TCapability>(IUIElementHandle target, string binderName)
            where TCapability : class
        {
            if (target == null)
            {
                UnityEngine.Debug.LogWarning($"[NexUI] {binderName}: target handle is null.");
                return null;
            }

            var cap = target.As<TCapability>();
            if (cap == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"[NexUI] {binderName}: element '{target.Id}' does not provide capability " +
                    $"'{typeof(TCapability).Name}'. Binding will be inactive.");
            }
            return cap;
        }
    }
}
