using System;

namespace emiteat.NexUI.State
{
    /// <summary>
    /// Links two <see cref="IBindableProperty{T}"/> instances. <see cref="Bind{T}"/> mirrors
    /// source changes onto target (one-way); <see cref="BindTwoWay{T}"/> additionally mirrors
    /// target changes back onto source, with a re-entrancy guard so a change can't bounce back
    /// and forth. UI-element bindings (<see cref="PropertyValueBinder{TSource}"/>,
    /// <see cref="PropertyTextBinder{TSource}"/>) are one-way only today because the element
    /// capabilities they read from (<see cref="Abstractions.IUIValueCapability"/>,
    /// <see cref="Abstractions.IUITextCapability"/>) expose no user-edit change event; two-way
    /// UI binding needs a new capability interface, which is an Abstractions-surface change and
    /// out of scope here.
    /// </summary>
    public static class PropertyBinding
    {
        public static IDisposable Bind<T>(IBindableProperty<T> source, IBindableProperty<T> target)
        {
            if (source == null || target == null) return null;

            void OnSourceChanged(T v) => target.Value = v;

            source.ValueChanged += OnSourceChanged;
            target.Value = source.Value;
            return new Unsubscriber(() => source.ValueChanged -= OnSourceChanged);
        }

        public static IDisposable BindTwoWay<T>(IBindableProperty<T> source, IBindableProperty<T> target)
        {
            if (source == null || target == null) return null;
            var syncing = false;

            void OnSourceChanged(T v)
            {
                if (syncing) return;
                syncing = true;
                target.Value = v;
                syncing = false;
            }

            void OnTargetChanged(T v)
            {
                if (syncing) return;
                syncing = true;
                source.Value = v;
                syncing = false;
            }

            source.ValueChanged += OnSourceChanged;
            target.ValueChanged += OnTargetChanged;
            target.Value = source.Value;

            return new Unsubscriber(() =>
            {
                source.ValueChanged -= OnSourceChanged;
                target.ValueChanged -= OnTargetChanged;
            });
        }

        private sealed class Unsubscriber : IDisposable
        {
            private Action _dispose;
            public Unsubscriber(Action dispose) => _dispose = dispose;
            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }
}
