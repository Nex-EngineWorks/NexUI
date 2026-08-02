using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Core.Validation;
using emiteat.NexUI.Tests.Fakes;

namespace emiteat.NexUI.Tests.EditMode
{
    public sealed class ValidatorEditModeTests
    {
        private static UIScreenDefinition Screen(string id, UnityEngine.Object asset = null)
        {
            var def = ScriptableObject.CreateInstance<UIScreenDefinition>();
            def.identity = new UIScreenIdentity { screenId = id };
            def.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI, asset = asset };
            return def;
        }

        [Test]
        public void CapabilityValidatorChecksLiveSurfaceAgainstContract()
        {
            var definition = ScriptableObject.CreateInstance<UIScreenDefinition>();
            definition.identity = new UIScreenIdentity { screenId = "pause" };
            definition.contract = ScriptableObject.CreateInstance<UIScreenContract>();
            definition.contract.screenId = "pause";
            definition.contract.requiredElements.Add(new UIElementContract
            {
                elementId = "resume",
                requiredCapabilities = new List<string> { nameof(IUIClickCapability), nameof(IUIInteractableCapability) }
            });

            var surface = new FakeSurface("pause").AddElement("resume",
                new FakeElementHandle("resume").With<IUIClickCapability>(new FakeClick()));
            var report = new UIValidationReport();
            new CapabilityBindingValidator().Validate(new UIValidationContext(new[] { definition },
                liveSurfaces: new Dictionary<string, IUISurface> { ["pause"] = surface }), report);

            Assert.AreEqual(1, report.ErrorCount, "The missing interactable capability must be reported.");
            Object.DestroyImmediate(definition.contract);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void DuplicateScreenIdValidator_FlagsDuplicates()
        {
            var defs = new List<UIScreenDefinition> { Screen("HUD"), Screen("HUD") };
            var report = new UIValidationReport();
            new DuplicateScreenIdValidator().Validate(new UIValidationContext(defs), report);
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void MissingAssetValidator_FlagsMissingAsset()
        {
            var defs = new List<UIScreenDefinition> { Screen("HUD", asset: null) };
            var report = new UIValidationReport();
            new MissingAssetValidator().Validate(new UIValidationContext(defs), report);
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void ProjectValidator_NoErrorsForCleanSingleScreen()
        {
            var def = Screen("HUD", asset: ScriptableObject.CreateInstance<UIScreenDefinition>());
            def.layer = new UIScreenLayerConfig { layerType = UILayerType.HUD, openPolicy = UIOpenPolicy.Single };
            var report = new ProjectValidator().Validate(new UIValidationContext(new[] { def }));
            Assert.AreEqual(0, report.ErrorCount);
        }
    }
}
