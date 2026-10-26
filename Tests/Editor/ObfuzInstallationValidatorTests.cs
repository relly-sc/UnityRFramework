using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor.Tests
{
    public sealed class ObfuzInstallationValidatorTests
    {
        private string root;
        private UnityRFrameworkBuildProfile profile;
        private BuildValidationContext context;
        private List<BuildValidationIssue> issues;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "UnityRFrameworkObfuzTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
            profile = ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            context = new BuildValidationContext(profile, BuildTarget.StandaloneWindows64, root, "", "", "", BuildRecipe.Player);
            issues = new List<BuildValidationIssue>();
            FakeSettings.Instance = new FakeSettings();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(profile);
            Directory.Delete(root, true);
        }

        [Test]
        public void NoPluginHasNoObfuzRequirement()
        {
            ObfuzInstallationValidator.ValidateInstallation(context, issues, null, false);
            Assert.IsEmpty(issues);
        }

        [Test]
        public void CorePreflightIsDiscoveredWithoutAnExpansionValidator()
        {
            BuildValidatorRegistry.InvalidateCache();
            Assert.IsTrue(BuildValidatorRegistry.GetAll().Any(v => v.Id == "core.obfuz-installation"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingSettingsBlocksPlayerWithoutCreatingOrChangingSettings(bool extension)
        {
            ObfuzInstallationValidator.ValidateInstallation(context, issues, typeof(FakeSettings), extension);
            Assert.That(issues.Any(i => i.Code == "OBFUZ_INIT"), Is.True);
            Assert.IsFalse(File.Exists(Path.Combine(root, "ProjectSettings", "Obfuz.asset")));
            Assert.IsTrue(FakeSettings.Instance.buildPipelineSettings.enable);
        }

        [Test]
        public void NullChildSettingsAreDiagnosedBeforePluginCallbacks()
        {
            WriteSettings();
            FakeSettings.Instance.buildPipelineSettings = null;
            ObfuzInstallationValidator.ValidateInstallation(context, issues, typeof(FakeSettings), true);
            Assert.That(issues.Single().Message, Does.Contain("buildPipelineSettings"));
            Assert.That(File.ReadAllText(Path.Combine(root, "ProjectSettings", "Obfuz.asset")), Is.EqualTo("test settings"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PluginWithoutExtensionReportsCallbackRisk(bool enabled)
        {
            WriteSettings();
            FakeSettings.Instance.buildPipelineSettings.enable = enabled;
            ObfuzInstallationValidator.ValidateInstallation(context, issues, typeof(FakeSettings), false);
            Assert.AreEqual("OBFUZ_EXTENSION", issues.Single().Code);
            Assert.AreEqual(enabled ? BuildValidationLevel.Error : BuildValidationLevel.Warning, issues.Single().Level);
            Assert.AreEqual(enabled, FakeSettings.Instance.buildPipelineSettings.enable);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InitializedExtensionLeavesActivationToExistingTransaction(bool enabled)
        {
            WriteSettings();
            if (enabled) profile.Steps.Add(new BuildStepSettings { StepId = "obfuz", Enabled = true });
            ObfuzInstallationValidator.ValidateInstallation(context, issues, typeof(FakeSettings), true);
            Assert.IsEmpty(issues);
            Assert.IsTrue(FakeSettings.Instance.buildPipelineSettings.enable);
        }

        [Test]
        public void AssetsOnlyRecipeDoesNotRequirePlayerCallbackInitialization()
        {
            context = new BuildValidationContext(profile, BuildTarget.StandaloneWindows64, root, "", "", "", BuildRecipe.Assets);
            ObfuzInstallationValidator.ValidateInstallation(context, issues, typeof(FakeSettings), false);
            Assert.IsEmpty(issues);
        }

        private void WriteSettings() => File.WriteAllText(Path.Combine(root, "ProjectSettings", "Obfuz.asset"), "test settings");

        private sealed class FakeSettings
        {
            public static FakeSettings Instance { get; set; }
            public FakePipeline buildPipelineSettings = new FakePipeline();
            public object assemblySettings = new object();
            public object encryptionVMSettings = new object();
            public object symbolObfusSettings = new object();
        }

        private sealed class FakePipeline { public bool enable = true; }
    }
}
