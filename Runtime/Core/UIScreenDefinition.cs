using UnityEngine;

namespace emiteat.NexUI.Core
{
    /// <summary>
    /// Authoring asset describing a single screen: its identity, backend asset,
    /// layer, motion, policy, focus, relations and validation configuration.
    /// This is the single source of truth the UIManager consumes to open a screen.
    /// </summary>
    [CreateAssetMenu(menuName = "NexUI/Screen Definition", fileName = "NewScreenDefinition")]
    public sealed class UIScreenDefinition : ScriptableObject
    {
        public UIScreenIdentity identity;
        public UIScreenBackendAsset backendAsset;
        public UIScreenLayerConfig layer;
        public UIScreenMotionConfig motion;
        public UIScreenPolicyConfig policy;
        public UIScreenFocusConfig focus;
        public UIScreenRelationConfig relations;
        public UIScreenValidationConfig validation;

        /// <summary>How the manager provisions this screen's backend instance.</summary>
        public UIScreenLoadStrategy loadStrategy;

        /// <summary>Named variations applied on top of the base screen at open time.</summary>
        public UIScreenVariant[] variants;

        /// <summary>Resolution / input-mode driven layout adaptations.</summary>
        public UIResponsiveRule[] responsiveRules;

        public string ScreenId => identity.screenId;
    }
}
