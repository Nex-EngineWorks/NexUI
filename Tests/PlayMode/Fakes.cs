using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using UnityEngine;

namespace emiteat.NexUI.Tests.Fakes
{
    public sealed class FakeText : IUITextCapability { public string Text { get; set; } }

    public sealed class FakeValue : IUIValueCapability
    {
        public float Value { get; set; }
        public float Min { get; set; }
        public float Max { get; set; } = 1f;
    }

    public sealed class FakeTextInput : IUITextInputCapability
    {
        public string Text { get; set; }
        public event Action<string> TextChanged;
        public void Raise(string value) { Text = value; TextChanged?.Invoke(value); }
    }

    public sealed class FakeValueInput : IUIValueInputCapability
    {
        public float Value { get; set; }
        public float Min { get; set; }
        public float Max { get; set; } = 1f;
        public event Action<float> ValueChanged;
        public void Raise(float value) { Value = value; ValueChanged?.Invoke(value); }
    }

    public sealed class FakeVisibility : IUIVisibilityCapability { public bool Visible { get; set; } = true; }
    public sealed class FakeInteractable : IUIInteractableCapability { public bool Interactable { get; set; } = true; }

    public sealed class FakeClick : IUIClickCapability
    {
        public event Action Clicked;
        public void Raise() => Clicked?.Invoke();
    }

    public sealed class FakeStyle : IUIStyleCapability
    {
        public readonly HashSet<string> Classes = new HashSet<string>();
        public readonly Dictionary<string, string> Tokens = new Dictionary<string, string>();
        public void SetClass(string c, bool on) { if (on) Classes.Add(c); else Classes.Remove(c); }
        public void ApplyToken(string k, string v) => Tokens[k] = v;
    }

    public sealed class FakeTransform : IUITransformCapability
    {
        public float Opacity { get; set; } = 1f;
        public Vector2 Position { get; set; }
        public Vector3 Scale { get; set; } = Vector3.one;
        public float Rotation { get; set; }
    }

    public sealed class FakeElementHandle : IUIElementHandle
    {
        private readonly Dictionary<Type, object> _caps = new Dictionary<Type, object>();
        public string Id { get; }
        public UIRenderBackend Backend { get; }
        public object Native => this;

        public FakeElementHandle(string id, UIRenderBackend backend = UIRenderBackend.UGUI)
        {
            Id = id; Backend = backend;
            Add<IUIVisibilityCapability>(new FakeVisibility());
            Add<IUITransformCapability>(new FakeTransform());
            Add<IUIStyleCapability>(new FakeStyle());
        }

        public FakeElementHandle With<T>(T cap) where T : class { Add(cap); return this; }
        private void Add<T>(T cap) where T : class => _caps[typeof(T)] = cap;
        public bool Has<T>() where T : class => _caps.ContainsKey(typeof(T));
        public T As<T>() where T : class => _caps.TryGetValue(typeof(T), out var c) ? c as T : null;
    }

    public sealed class FakeSurface : IUISurface
    {
        private readonly Dictionary<string, IUIElementHandle> _elements = new Dictionary<string, IUIElementHandle>();
        public string ScreenId { get; }
        public UIRenderBackend Backend { get; }
        public object NativeRoot => this;
        public IUIElementHandle RootHandle { get; }
        public bool Active { get; private set; }
        public bool Destroyed { get; private set; }

        public FakeSurface(string screenId, UIRenderBackend backend = UIRenderBackend.UGUI)
        {
            ScreenId = screenId; Backend = backend;
            RootHandle = new FakeElementHandle(screenId, backend);
        }

        public FakeSurface AddElement(string id, IUIElementHandle handle) { _elements[id] = handle; return this; }

        public IUIElementHandle TryFind(string elementId) => _elements.TryGetValue(elementId, out var h) ? h : null;
        public IUIElementHandle FindRequired(string elementId) => TryFind(elementId) ?? throw new UIElementNotFoundException(elementId);
        public void SetActive(bool active) => Active = active;
        public void SetSortingOrder(int order) { }
        public void SetInputBlocking(bool blocking) { }
        public void Destroy() => Destroyed = true;
    }

    public sealed class FakeScreenFactory : IUIScreenFactory
    {
        private readonly UIRenderBackend _backend;
        public FakeSurface Last { get; private set; }
        public FakeScreenFactory(UIRenderBackend backend = UIRenderBackend.UGUI) => _backend = backend;
        public UIRenderBackend Backend => _backend;
        public UniTask<IUISurface> CreateAsync(UIScreenDefinition definition, IUISurface parentLayer, CancellationToken ct)
        {
            Last = new FakeSurface(definition.ScreenId, _backend);
            return UniTask.FromResult<IUISurface>(Last);
        }
    }

    public sealed class FakeFocusAdapter : IUIFocusAdapter
    {
        public int TrapCount, ReleaseCount;
        public UIRenderBackend Backend { get; }
        public FakeFocusAdapter(UIRenderBackend backend = UIRenderBackend.UGUI) => Backend = backend;
        public void Trap(IUISurface surface, string defaultElementId) => TrapCount++;
        public void Release(IUISurface surface, bool restorePrevious) => ReleaseCount++;
    }

    public sealed class FakeThemeApplier : IUIThemeApplier
    {
        public readonly List<(string key, string value)> Applied = new List<(string, string)>();
        public UIRenderBackend Backend { get; }
        public FakeThemeApplier(UIRenderBackend backend = UIRenderBackend.UGUI) => Backend = backend;
        public void ApplyToken(IUIElementHandle target, string tokenKey, string value) => Applied.Add((tokenKey, value));
    }
}
