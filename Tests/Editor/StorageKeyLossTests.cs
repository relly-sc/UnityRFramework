using System;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using RFramework;
using UnityEngine;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>只在独立临时目录模拟丢钥与重启，不读取或删除用户密钥。</summary>
    public sealed class StorageKeyLossTests
    {
        private string root;
        private GameObject owner;
        private StorageComponent component;
        private FileKeyStore keys;
        private DefaultStorageHelper helper;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "UnityRFrameworkKeyLossTests", Guid.NewGuid().ToString("N"));
            keys = new FileKeyStore(Path.Combine(root, "Keys"));
            CreateComponent();
        }

        [TearDown]
        public void TearDown()
        {
            RFrameworkModuleHost.StopAll();
            UnityEngine.Object.DestroyImmediate(owner);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RestartWithLostKeyDoesNotCreateSameKeyOrOverwriteSave(bool backupOnly)
        {
            component.SetManagedSaveKeyStore(keys);
            Assert.IsTrue(component.SaveAsync("slot", "backup").GetAwaiter().GetResult().Succeeded);
            Assert.IsTrue(component.SaveAsync("slot", "primary").GetAwaiter().GetResult().Succeeded);
            if (backupOnly) File.Delete(Path.Combine(root, "Saves", "slot.save"));
            byte[] backup = File.ReadAllBytes(Path.Combine(root, "Saves", "slot.save.bak"));
            byte[] primary = backupOnly ? null : File.ReadAllBytes(Path.Combine(root, "Saves", "slot.save"));
            Assert.IsTrue(keys.Delete("SaveKey"));
            CreateComponent();

            Assert.Throws<RFrameworkException>(() => component.SetManagedSaveKeyStore(keys));
            Assert.IsFalse(keys.TryRead("SaveKey", out _), "丢钥后不得重建同名密钥。");
            Assert.IsNull(component.ManagedSaveKeyProvider);
            Assert.IsNotEmpty(component.ManagedSaveKeyStoreError);
            var load = backupOnly ? component.LoadBackupAsync<string>("slot") : component.LoadAsync<string>("slot");
            Assert.AreEqual(StorageExceptionReason.KeyUnavailable, load.GetAwaiter().GetResult().Reason);
            Assert.Throws<RFrameworkException>(() => component.SaveAsync("slot", "replacement", new StorageOptions()));
            Assert.Throws<RFrameworkException>(() => component.SaveAutoAsync("replacement"));
            CollectionAssert.AreEqual(backup, File.ReadAllBytes(Path.Combine(root, "Saves", "slot.save.bak")));
            if (primary != null) CollectionAssert.AreEqual(primary, File.ReadAllBytes(Path.Combine(root, "Saves", "slot.save")));
            else Assert.IsFalse(File.Exists(Path.Combine(root, "Saves", "slot.save")));
        }

        [Test]
        public void FirstInstallCreatesKeyAndRestartReusesIt()
        {
            component.SetManagedSaveKeyStore(keys);
            Assert.IsTrue(keys.TryRead("SaveKey", out byte[] original));
            try
            {
                Assert.IsTrue(component.SaveAsync("slot", "saved").GetAwaiter().GetResult().Succeeded);
                CreateComponent();
                component.SetManagedSaveKeyStore(keys);
                Assert.IsTrue(keys.TryRead("SaveKey", out byte[] restarted));
                try { CollectionAssert.AreEqual(original, restarted); }
                finally { Array.Clear(restarted, 0, restarted.Length); }
                Assert.AreEqual("saved", component.LoadAsync<string>("slot").GetAwaiter().GetResult().Data);
            }
            finally { Array.Clear(original, 0, original.Length); }
        }

        [Test]
        public void ExplicitRotationPreservesOldKeyAndLoadsOldBackup()
        {
            component.SetManagedSaveKeyStore(keys);
            Assert.IsTrue(component.SaveAsync("slot", "old").GetAwaiter().GetResult().Succeeded);
            Assert.IsTrue(component.ManagedSaveKeyProvider.RotateKey("SaveKey.v2"));
            component.SetManagedSaveKeyStore(keys, "SaveKey.v2");
            Assert.IsTrue(component.SaveAsync("slot", "new").GetAwaiter().GetResult().Succeeded);
            Assert.IsTrue(keys.TryRead("SaveKey", out byte[] oldKey));
            Array.Clear(oldKey, 0, oldKey.Length);
            var backup = component.LoadBackupAsync<string>("slot").GetAwaiter().GetResult();
            Assert.IsTrue(backup.Succeeded, backup.Message);
            Assert.IsTrue(backup.RecoveredFromBackup);
            Assert.AreEqual("old", backup.Data);
        }

        [Test]
        public void MissingRotationKeyIsNotAutomaticallyCreatedForAnExistingSave()
        {
            component.SetManagedSaveKeyStore(keys);
            component.SaveAsync("slot", "old").GetAwaiter().GetResult();
            Assert.Throws<RFrameworkException>(() => component.SetManagedSaveKeyStore(keys, "SaveKey.v2"));
            Assert.IsFalse(keys.TryRead("SaveKey.v2", out _));
            component.SetManagedSaveKeyStore(keys);
            Assert.IsEmpty(component.ManagedSaveKeyStoreError);
            Assert.AreEqual("old", component.LoadAsync<string>("slot").GetAwaiter().GetResult().Data);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MalformedOrUnreadableKeyIsNotReplaced(bool malformed)
        {
            component.SetManagedSaveKeyStore(keys);
            component.SaveAsync("slot", "saved").GetAwaiter().GetResult();
            string keyPath = Directory.GetFiles(Path.Combine(root, "Keys"), "*.key")[0];
            if (malformed) File.WriteAllBytes(keyPath, new byte[] { 1, 2, 3 });
            byte[] original = File.ReadAllBytes(keyPath);
            IKeyStore store = malformed ? keys : (IKeyStore)new UnreadableKeyStore();
            CreateComponent();
            Assert.Throws<RFrameworkException>(() => component.SetManagedSaveKeyStore(store));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(keyPath));
            Assert.Throws<RFrameworkException>(() => component.SaveAsync("slot", "replacement"));
        }

        [Test]
        public void PlainSaveAlsoRequiresExplicitDecisionBeforeCreatingAKey()
        {
            Assert.IsTrue(component.SaveAsync("slot", "plain", new StorageOptions()).GetAwaiter().GetResult().Succeeded);
            byte[] original = File.ReadAllBytes(Path.Combine(root, "Saves", "slot.save"));
            Assert.Throws<RFrameworkException>(() => component.SetManagedSaveKeyStore(keys));
            Assert.IsFalse(keys.TryRead("SaveKey", out _));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(Path.Combine(root, "Saves", "slot.save")));
        }

        [Test]
        public void RemovedTestSavesAllowNewInstallKeyWithoutTouchingOldFiles()
        {
            component.SetManagedSaveKeyStore(keys);
            component.SaveAsync("slot", "saved").GetAwaiter().GetResult();
            keys.Delete("SaveKey");
            component.DeleteAsync("slot").GetAwaiter().GetResult();
            CreateComponent();
            Assert.DoesNotThrow(() => component.SetManagedSaveKeyStore(keys));
            Assert.IsTrue(keys.TryRead("SaveKey", out byte[] generated));
            Array.Clear(generated, 0, generated.Length);
        }

        [Test]
        public void SwappedLocalHelperUsesItsActualDirectoryInsteadOfComponentDisplayPath()
        {
            helper.Initialize(Path.Combine(root, "OtherSaves"));
            helper.WriteAtomicAsync("slot", new byte[] { 1, 2, 3 }, true, default).GetAwaiter().GetResult();
            Assert.Throws<RFrameworkException>(() => component.SetManagedSaveKeyStore(keys));
            Assert.IsFalse(keys.TryRead("SaveKey", out _));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnreadableLocalStorageDoesNotCountAsAnEmptyInstall(bool replaceWithFile)
        {
            string saves = Path.Combine(root, "Saves");
            Directory.Delete(saves);
            if (replaceWithFile) File.WriteAllText(saves, "directory replaced by a file");
            Assert.Catch<IOException>(() => component.SetManagedSaveKeyStore(keys));
            Assert.IsFalse(keys.TryRead("SaveKey", out _));
            Assert.IsNotEmpty(component.ManagedSaveKeyStoreError);
        }

        private sealed class UnreadableKeyStore : IKeyStore
        {
            public bool TryRead(string id, out byte[] key) => throw new RFrameworkException("Test key store cannot decrypt its persisted key.");
            public void Write(string id, byte[] key) => Assert.Fail("不可解密的密钥仓不允许写入新密钥。");
            public bool Delete(string id) => throw new NotSupportedException();
        }

        private sealed class TextSerializer : IStorageSerializer
        {
            public byte[] Serialize<T>(T data) => Encoding.UTF8.GetBytes((string)(object)data);
            public T Deserialize<T>(byte[] data) => (T)(object)Encoding.UTF8.GetString(data);
        }

        private void CreateComponent()
        {
            RFrameworkModuleHost.StopAll();
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            owner = new GameObject("StorageKeyLossTest");
            owner.SetActive(false);
            component = owner.AddComponent<StorageComponent>();
            helper = owner.AddComponent<DefaultStorageHelper>();
            string saves = Path.Combine(root, "Saves");
            helper.Initialize(saves);
            var module = RFrameworkModuleHost.Get<IStorageModule>();
            module.SetSerializer(new TextSerializer());
            SetField("storageModule", module);
            SetField("<StorageRootPath>k__BackingField", saves);
            SetField("defaultProtectionMode", StorageProtectionMode.EncryptedAndAuthenticated);
            component.SetHelper(helper);
        }

        private void SetField(string name, object value) => typeof(StorageComponent)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, value);
    }
}
