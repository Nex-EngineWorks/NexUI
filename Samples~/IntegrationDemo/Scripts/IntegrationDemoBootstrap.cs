using System;
using UnityEngine;
using emiteat.NexUI.Core;
using emiteat.NexUI.Settings;

namespace emiteat.NexUI.Samples.IntegrationDemo
{
    public sealed class IntegrationDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private NexUISettings _settings;

        private void Start()
        {
            if (_settings != null)
            {
                foreach (var screen in _settings.screens)
                    if (screen != null) NexUI.RegisterScreen(screen);
            }

            Debug.Log($"[NexUI IntegrationDemo] DOTween integration: {Available("emiteat.NexUI.Integrations.DOTween.DOTweenMotionPlayer, emiteat.NexUI.Integrations.DOTween")}");
            Debug.Log($"[NexUI IntegrationDemo] VContainer integration: {Available("emiteat.NexUI.Integrations.VContainer.NexUIVContainerExtensions, emiteat.NexUI.Integrations.VContainer")}");
            Debug.Log($"[NexUI IntegrationDemo] MessagePipe integration: {Available("emiteat.NexUI.Integrations.MessagePipe.NexUIMessagePublisher, emiteat.NexUI.Integrations.MessagePipe")}");
            Debug.Log($"[NexUI IntegrationDemo] Addressables integration: {Available("emiteat.NexUI.Integrations.Addressables.AddressablesUIResourceProvider, emiteat.NexUI.Integrations.Addressables")}");
            Debug.Log($"[NexUI IntegrationDemo] Input System integration: {Available("emiteat.NexUI.Integrations.InputSystem.InputSystemPolicy, emiteat.NexUI.Integrations.InputSystem")}");
        }

        private static string Available(string assemblyQualifiedType)
            => Type.GetType(assemblyQualifiedType) != null ? "available" : "not compiled";
    }
}
