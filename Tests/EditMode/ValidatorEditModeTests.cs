using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Core.Validation;

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
