using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.State
{
    /// <summary>
    /// Base for one-way bindings between an <see cref="IBindableProperty{T}"/> data source and an
    /// element capability - the direct-reference counterpart to <see cref="UIBinder"/> (which
    /// binds through a <see cref="UIStateStore"/> string key instead of a property reference).
    /// </summary>
    public abstract class PropertyBinder<T>
    {
        public abstract void Bind(IUIElementHandle target, IBindableProperty<T> source);
        public abstract void Unbind();

        /// <summary>Helper: resolve a required capability, logging a warning (not silently failing) when absent.</summary>
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
