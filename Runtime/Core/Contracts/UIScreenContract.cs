using System;
using System.Collections.Generic;
using UnityEngine;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Declares the elements and capabilities a screen must provide. Used by the
    /// Designer contract checker and by runtime validation to guarantee that a
    /// screen fulfils its interaction contract (e.g. a PauseMenu must expose a
    /// resumeButton with a click capability). Capabilities are referenced by the
    /// interface type name (e.g. "IUIClickCapability") so Core stays decoupled.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Contract/Screen Contract", fileName = "NewScreenContract")]
    public sealed class UIScreenContract : ScriptableObject
    {
        public string screenId;
        public List<UIElementContract> requiredElements = new();
    }

    /// <summary>A required element within a <see cref="UIScreenContract"/>.</summary>
    [Serializable]
    public sealed class UIElementContract
    {
        public string elementId;
        public List<string> requiredCapabilities = new();
        public bool required = true;
    }
}
