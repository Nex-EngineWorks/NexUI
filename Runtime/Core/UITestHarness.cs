using System.Threading.Tasks;
using emiteat.NexUI.Abstractions;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// B9: surfaces the Command Pipeline as a test-facing entry point. NexUI already routes every
    /// UI interaction through ScreenID + <see cref="IUICommand"/> instead of touching
    /// GameObjects/VisualElements directly - most Unity UI test setups instead rely on
    /// <c>GameObject.Find</c> plus coordinate-based click simulation, which breaks whenever
    /// camera/resolution settings differ. This class doesn't add new capability; it just exposes
    /// the existing <see cref="UIManager"/>/<see cref="IUICommandDispatcher"/> calls under names
    /// that read naturally from a test, plus element lookup for assertions.
    /// </summary>
    public sealed class UITestHarness
    {
        private readonly UIManager _manager;
        private readonly IUICommandDispatcher _dispatcher;

        public UITestHarness(UIManager manager, IUICommandDispatcher dispatcher)
        {
            _manager = manager;
            _dispatcher = dispatcher;
        }

        public Task OpenAsync(string screenId, UIOpenArgs args = default) => _manager.OpenAsync(screenId, args);
        public Task CloseAsync(string screenId, UICloseArgs args = default) => _manager.CloseAsync(screenId, args);
        public Task ToggleAsync(string screenId) => _manager.ToggleAsync(screenId);
        public bool IsOpen(string screenId) => _manager.IsOpen(screenId);

        /// <summary>Dispatches a command through the same middleware chain a real click would (e.g. <c>new OpenScreenCommand("Settings")</c>).</summary>
        public Task InvokeAsync(IUICommand command) => _dispatcher.DispatchAsync(command);

        /// <summary>Finds an element on an open screen by id, for assertions (reading text/value/visibility capabilities) - never a GameObject.Find or coordinate hit-test.</summary>
        public IUIElementHandle FindElement(string screenId, string elementId)
            => _manager.GetSurface(screenId)?.TryFind(elementId);

        /// <summary>Convenience: reads a text-capable element's current text, or null if the screen/element/capability isn't present.</summary>
        public string ReadText(string screenId, string elementId)
            => FindElement(screenId, elementId)?.As<IUITextCapability>()?.Text;

        /// <summary>Convenience: reads a value-capable element's current value (progress bars, sliders), or 0 if not present.</summary>
        public float ReadValue(string screenId, string elementId)
            => FindElement(screenId, elementId)?.As<IUIValueCapability>()?.Value ?? 0f;

        /// <summary>
        /// There is intentionally no "simulate a click" method here: <see cref="IUIClickCapability.Clicked"/>
        /// is a subscribe-only event with no external trigger, by design (raising it would need a
        /// backend-specific cast, which is exactly what this harness exists to avoid). Drive the
        /// interaction the same way a click would: dispatch the command that button's
        /// <c>commandKey</c> is bound to via <see cref="InvokeAsync"/> - e.g.
        /// <c>harness.InvokeAsync(new OpenScreenCommand("Settings"))</c> for a button that opens
        /// the Settings screen. That is the same ScreenID+Command path the click ends up calling.
        /// </summary>
    }
}
