using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Core
{
    // ---- Policy enums -------------------------------------------------------

    public enum CursorPolicy
    {
        Unchanged = 0,
        ForceVisible = 1,
        ForceHidden = 2,
        LockCentered = 3
    }

    public enum UITimePolicy
    {
        Unchanged = 0,
        PauseWhileOpen = 1,
        SlowWhileOpen = 2
    }

    public enum UIFocusPolicy
    {
        None = 0,
        AutoFocusDefault = 1,
        TrapFocus = 2
    }

    public enum UITransitionConflictPolicy
    {
        /// <summary>Wait for the in-flight transition to finish, then run.</summary>
        Wait = 0,

        /// <summary>Cancel the in-flight transition and run immediately.</summary>
        Cancel = 1,

        /// <summary>Drop the new request while a transition is running.</summary>
        Ignore = 2
    }

    public enum UILifetimePolicy
    {
        DestroyOnClose = 0,
        KeepAlive = 1,
        Pool = 2
    }

    // ---- Serializable config blocks ----------------------------------------

    [Serializable]
    public struct UIScreenIdentity
    {
        public string screenId;
        public int priority;

        /// <summary>
        /// Screen-reader-facing label announced when this screen opens (e.g. "Settings").
        /// Plain string (not the <c>AccessibilityRole</c> enum) so Core doesn't need a new
        /// dependency on the Accessibility assembly for a single field.
        /// </summary>
        public string accessibilityLabel;
    }

    /// <summary>
    /// Backend + asset references. NOTE: assets are stored as <see cref="UnityEngine.Object"/>
    /// so Core never has a compile dependency on UI Toolkit / uGUI / Motion asset types.
    /// The Integration casts them to their concrete backend type.
    /// </summary>
    [Serializable]
    public struct UIScreenBackendAsset
    {
        public emiteat.NexUI.Abstractions.UIRenderBackend backend;
        public UnityEngine.Object asset;
        public UnityEngine.Object[] styleAssets;
    }

    [Serializable]
    public struct UIScreenLayerConfig
    {
        public UILayerType layerType;
        public UIOpenPolicy openPolicy;
    }

    /// <summary>
    /// Motion assets are referenced as <see cref="UnityEngine.Object"/> (a UIMotionPreset
    /// lives in the Motion assembly which Core must not reference). At runtime an
    /// <see cref="emiteat.NexUI.Abstractions.IUIMotionResolver"/> compiles them to a timeline.
    /// </summary>
    [Serializable]
    public struct UIScreenMotionConfig
    {
        public UnityEngine.Object openMotion;
        public UnityEngine.Object closeMotion;
    }

    [Serializable]
    public struct UIScreenPolicyConfig
    {
        public bool blockInputBehind;
        public bool pauseGameBehind;
        public bool closeOnBack;

        public CursorPolicy cursorPolicy;
        public UITimePolicy timePolicy;
        public UIFocusPolicy focusPolicy;
        public UITransitionConflictPolicy conflictPolicy;
        public UILifetimePolicy lifetimePolicy;
    }

    [Serializable]
    public struct UIScreenFocusConfig
    {
        public string defaultFocusElementId;
        public bool trapFocus;
        public bool restoreFocusOnClose;
    }

    [Serializable]
    public struct UIScreenRelationConfig
    {
        /// <summary>Screens that should be opened together with this one.</summary>
        public string[] opensWith;

        /// <summary>Screens that must be closed when this one opens.</summary>
        public string[] closes;

        /// <summary>Parent screen id (for nested / owned screens).</summary>
        public string parentScreenId;
    }

    [Serializable]
    public struct UIScreenValidationConfig
    {
        public bool requireDefaultFocusForModal;
        public bool warnOnMissingMotion;
        public bool treatWarningsAsErrors;
    }

    // ---- Runtime call arguments --------------------------------------------

    /// <summary>Arguments for an open request. Payload is an untyped bag for user data.</summary>
    public struct UIOpenArgs
    {
        public bool suppressMotion;
        public IReadOnlyDictionary<string, object> payload;

        /// <summary>Optional screen variant to open (see <see cref="UIScreenVariant"/>).
        /// Null/empty opens the base screen.</summary>
        public string variantId;

        public static UIOpenArgs None => default;
    }

    /// <summary>Arguments for a close request.</summary>
    public struct UICloseArgs
    {
        public bool suppressMotion;
        public bool immediate;

        public static UICloseArgs None => default;
    }
}
