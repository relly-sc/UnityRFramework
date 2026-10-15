using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>验证依赖顺序、版本固定、安装授权及缺失生成代码保护，不改写项目插件。</summary>
    public sealed class FrameworkSamplesInstallerTests
    {
        private FrameworkSampleCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            string script = AssetDatabase.FindAssets("FrameworkSampleCatalog t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath).Single();
            catalog = FrameworkSampleCatalog.Load(Path.Combine(Path.GetDirectoryName(script), "FrameworkSamples.json"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DevelopmentSource_UsesRepositoryLayoutInsteadOfProjectNameOrImportedSamples(bool development)
        {
            string root = Path.Combine(Path.GetTempPath(), "FrameworkSamplesTests", Guid.NewGuid().ToString("N"));
            string catalogPath = Path.Combine(root, "Scripts", "Editor", "FrameworkSamples.json");
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Samples"));
                Directory.CreateDirectory(Path.Combine(root, "Samples~"));
                if (development)
                {
                    Directory.CreateDirectory(Path.Combine(root, "Tools~"));
                    File.WriteAllText(Path.Combine(root, "Tools~", "SamplesPackage.json"), "{}");
                }
                Assert.That(FrameworkSampleCatalog.GetDevelopmentSamplesRoot(catalogPath),
                    Is.EqualTo(development ? Path.Combine(root, "Samples") : null));
                if (development)
                    Assert.Throws<InvalidOperationException>(() => FrameworkSamplesInstaller.Start(
                        catalogPath, new[] { "Sample.Demo" }, true, true));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void EverySample_HasDescriptionBeforeSamplesPackageIsInstalled()
        {
            foreach (var sample in catalog.samples)
                Assert.That(string.IsNullOrWhiteSpace(sample.description), Is.False, sample.name);
        }

        [Test]
        public void HybridClrDemo_ResolvesOnlyRequiredPackagesAndSamplesInOrder()
        {
            var plan = catalog.Resolve(new[] { "Expansion.HybridCLR.Demo", "Expansion.HybridCLR" });
            CollectionAssert.AreEqual(new[] { "Sample.Demo", "Expansion.YooAsset", "Expansion.UniTask",
                "Expansion.YooAsset.Demo", "Expansion.HybridCLR", "Expansion.HybridCLR.Demo" }, plan.Select(s => s.name));
            CollectionAssert.AreEquivalent(new[] { "com.tuyoogame.yooasset", "com.cysharp.unitask", "com.code-philosophy.hybridclr" },
                catalog.GetPackages(plan).Select(p => p.name));
            Assert.That(catalog.GetPackages(plan).Single(p => p.name.EndsWith("hybridclr")).version, Is.EqualTo("8.13.0"));
        }

        [Test]
        public void ObfuzPlayer_InstallsNoHybridClrAndJointExtensionResolvesBoth()
        {
            var ordinary = catalog.Resolve(new[] { "Expansion.Obfuz" });
            CollectionAssert.AreEqual(new[] { "Expansion.Obfuz" }, ordinary.Select(s => s.name));
            CollectionAssert.AreEqual(new[] { "com.code-philosophy.obfuz" }, catalog.GetPackages(ordinary).Select(p => p.name));
            var joint = catalog.Resolve(new[] { "Expansion.Obfuz.HybridCLR" });
            CollectionAssert.AreEqual(new[] { "Expansion.Obfuz", "Expansion.HybridCLR", "Expansion.Obfuz.HybridCLR" }, joint.Select(s => s.name));
            CollectionAssert.AreEquivalent(new[] { "com.code-philosophy.obfuz", "com.code-philosophy.hybridclr", "com.code-philosophy.obfuz4hybridclr" }, catalog.GetPackages(joint).Select(p => p.name));
        }

        [Test]
        public void CoreSample_DoesNotInstallThirdPartyPackages()
        {
            Assert.That(catalog.GetPackages(catalog.Resolve(new[] { "Sample.Demo" })), Is.Empty);
            Assert.That(catalog.GetPackages(catalog.Resolve(new[] { "Expansion.UI.Demo" })), Is.Empty);
        }

        [Test]
        public void PackageVersion_RejectsNewerVersionsAndUnpinnedGitBranches()
        {
            var dependency = catalog.packages.Single(p => p.name == "com.code-philosophy.hybridclr");
            Assert.That(dependency.Matches("8.13.0", PackageSource.Git, "v8.13.0"), Is.True);
            Assert.That(dependency.Matches("9.0.3", PackageSource.Git, "v9.0.3"), Is.False);
            Assert.That(dependency.Matches("8.13.0", PackageSource.Git, "main"), Is.False);
            Assert.That(dependency.Matches("8.13.0", PackageSource.Embedded, null), Is.True);
        }

        [Test]
        public void Changes_NeedApprovalAndNeverOverwriteLocalSourcesOrRetryFailedVersion()
        {
            var dependency = catalog.packages[0];
            var changes = new[] { dependency };
            Assert.Throws<InvalidOperationException>(() => FrameworkSamplesInstaller.ValidateChanges(changes,
                Array.Empty<string>(), false, _ => null));
            foreach (var source in new[] { PackageSource.Local, PackageSource.Embedded })
                Assert.Throws<InvalidOperationException>(() => FrameworkSamplesInstaller.ValidateChanges(changes,
                    new[] { dependency.url }, false, _ => source));
            Assert.Throws<InvalidOperationException>(() => FrameworkSamplesInstaller.ValidateChanges(changes,
                new[] { dependency.url }, true, _ => PackageSource.Git));
            Assert.DoesNotThrow(() => FrameworkSamplesInstaller.ValidateChanges(changes,
                new[] { dependency.url }, false, _ => PackageSource.Git));
        }

        [Test]
        public void Resolve_RejectsMissingAndCircularDependencies()
        {
            Assert.Throws<InvalidDataException>(() => catalog.Resolve(new[] { "unknown" }));
            var broken = new FrameworkSampleCatalog
            {
                frameworkVersion = "test", packages = Array.Empty<FrameworkPackageDependency>(),
                samples = new[] { new FrameworkSampleDefinition { name = "a", dependencies = new[] { "b" } },
                    new FrameworkSampleDefinition { name = "b", dependencies = new[] { "a" } } }
            };
            Assert.Throws<InvalidDataException>(() => broken.Validate());
        }

        [Test]
        public void ResumedPlan_DoesNotExpandApprovedImportsAfterCatalogChanges()
        {
            var plan = catalog.Resolve(new[] { "Expansion.HybridCLR.Demo" });
            string[] names = plan.Select(s => s.name).ToArray();
            Assert.DoesNotThrow(() => FrameworkSamplesInstaller.ValidatePlan(catalog.frameworkVersion,
                catalog.frameworkVersion, catalog.samplesVersion, catalog.samplesVersion, names, plan));
            Assert.Throws<InvalidOperationException>(() => FrameworkSamplesInstaller.ValidatePlan("old",
                catalog.frameworkVersion, catalog.samplesVersion, catalog.samplesVersion, names, plan));
            Assert.Throws<InvalidOperationException>(() => FrameworkSamplesInstaller.ValidatePlan(catalog.frameworkVersion,
                catalog.frameworkVersion, "old", catalog.samplesVersion, names, plan));
            Assert.Throws<InvalidOperationException>(() => FrameworkSamplesInstaller.ValidatePlan(catalog.frameworkVersion,
                catalog.frameworkVersion, catalog.samplesVersion, catalog.samplesVersion, names.Take(1).ToArray(), plan));
        }

        [Test]
        public void SamplesVersion_IsIndependentAndSupportsNewerCompatibleCore()
        {
            Assert.That(catalog.samplesVersion, Is.Not.EqualTo(string.Empty));
            Assert.That(FrameworkSampleCatalog.IsVersionAtLeast("1.12.2", catalog.samplesMinimumFrameworkVersion), Is.True);
            Assert.That(FrameworkSampleCatalog.IsVersionAtLeast("1.12.0", catalog.samplesMinimumFrameworkVersion), Is.False);
        }

        [Test]
        public void NonUpmPrerequisites_BlockImportUntilMatchingAssemblyOrGeneratedTypeExists()
        {
            var r3 = catalog.samples.Single(s => s.name == "Expansion.R3");
            Assert.That(FrameworkSampleCatalog.GetPrerequisiteError(r3, _ => null, _ => false), Is.Not.Null);
            Assert.That(FrameworkSampleCatalog.GetPrerequisiteError(r3, _ => "2.0.0.0", _ => false), Is.Not.Null);
            Assert.That(FrameworkSampleCatalog.GetPrerequisiteError(r3, _ => "1.3.1.0", _ => false), Is.Null);
            var obfuz = catalog.samples.Single(s => s.name == "Expansion.Obfuz");
            Assert.That(FrameworkSampleCatalog.GetPrerequisiteError(obfuz, _ => null, _ => false), Is.Not.Null);
            Assert.That(FrameworkSampleCatalog.GetPrerequisiteError(obfuz, _ => null, _ => true), Is.Null);
        }

        [Test]
        public void SamplesUrl_PinsSamplesVersionInsteadOfLatestMain()
        {
            Assert.That(SamplesPackageInstaller.GetSamplesUrl("1.11.0"), Does.EndWith("#versions/1.11.0"));
            Assert.Throws<ArgumentException>(() => SamplesPackageInstaller.GetSamplesUrl(" "));
        }

        [Test]
        public void PreviousImport_RequiresExplicitOverwriteAndDoesNotDeleteUserFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "FrameworkSamplesTests", Guid.NewGuid().ToString("N"));
            string previous = Path.Combine(root, "1.10.0", "Expansion.UI");
            string current = Path.Combine(root, "1.11.0", "Expansion.UI");
            try
            {
                Directory.CreateDirectory(previous);
                string userFile = Path.Combine(previous, "User.cs");
                File.WriteAllText(userFile, "user-owned");
                Assert.Throws<InvalidOperationException>(() => FrameworkSamplesInstaller.EnsureNoPreviousImports(current));
                Assert.That(File.ReadAllText(userFile), Is.EqualTo("user-owned"));
                Assert.DoesNotThrow(() => FrameworkSamplesInstaller.EnsureNoPreviousImports(previous));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [UnityTest]
        public IEnumerator Window_RepaintsAtSmallAndLargeSizesWithoutGuiErrors()
        {
            var window = ScriptableObject.CreateInstance<FrameworkSamplesWindow>();
            try
            {
                window.titleContent = new GUIContent("扩展与示例");
                window.Show();
                window.Focus();
                var sourceField = typeof(FrameworkSamplesWindow).GetField("developmentSamplesRoot", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (string sourceRoot in new[] { (string)sourceField.GetValue(window), null }.Distinct())
                foreach (var view in new[] { (width: 480, height: 360, scroll: 0), (width: 960, height: 680, scroll: 0),
                    (width: 480, height: 360, scroll: 650) })
                {
                    sourceField.SetValue(window, sourceRoot);
                    window.position = new Rect(80, 80, view.width, view.height);
                    typeof(FrameworkSamplesWindow).GetField("scroll", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(window, new Vector2(0, view.scroll));
                    window.Repaint();
                    yield return null;
                    yield return null;
                    LogAssert.NoUnexpectedReceived();
                    if (Application.isBatchMode) continue;
                    var image = new Texture2D(view.width, view.height, TextureFormat.RGB24, false);
                    try
                    {
                        image.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(
                            window.position.position, image.width, image.height));
                        image.Apply();
                        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                            "Temp", $"扩展安装窗口-{(sourceRoot == null ? "消费级" : "开发")}-{image.width}-{view.scroll}.png");
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        File.WriteAllBytes(path, image.EncodeToPNG());
                    }
                    finally { UnityEngine.Object.DestroyImmediate(image); }
                }
            }
            finally { window.Close(); }
        }
    }
}
