using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.State
{
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
