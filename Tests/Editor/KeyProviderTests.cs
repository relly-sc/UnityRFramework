using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RFramework;
using UnityEditor;
using UnityEngine;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>安装级存档密钥的生成、轮换与删除契约测试。</summary>
    public sealed class KeyProviderTests
    {
        [Test]
        public void MissingLookupDoesNotCreateKey()
        {
            var store = new MemoryKeyStore();
            var provider = new InstallSaveKeyProvider(store);

            Assert.IsFalse(provider.TryGetKey(InstallSaveKeyProvider.DefaultKeyId, out byte[] key));
            Assert.IsNull(key);
            Assert.AreEqual(0, store.Count);
        }

        [Test]
        public void EnsureCreatesOnePersistentRandomKey()
        {
            var store = new MemoryKeyStore();
            var first = new InstallSaveKeyProvider(store);

            Assert.IsTrue(first.EnsureKey());
            Assert.IsFalse(first.EnsureKey());
            Assert.IsTrue(first.TryGetKey(InstallSaveKeyProvider.DefaultKeyId, out byte[] key));
            Assert.AreEqual(InstallSaveKeyProvider.KeySize, key.Length);

            var restarted = new InstallSaveKeyProvider(store);
            Assert.IsTrue(restarted.TryGetKey(InstallSaveKeyProvider.DefaultKeyId, out byte[] restored));
            CollectionAssert.AreEqual(key, restored);
            Array.Clear(key, 0, key.Length);
            Array.Clear(restored, 0, restored.Length);
        }

        [Test]
        public void RotationKeepsOldKeyUntilExplicitDeletion()
        {
            var store = new MemoryKeyStore();
            var provider = new InstallSaveKeyProvider(store);
            provider.EnsureKey();

            Assert.IsTrue(provider.RotateKey("SaveKey.v2"));
            Assert.IsTrue(provider.TryGetKey("SaveKey", out byte[] oldKey));
            Assert.IsTrue(provider.TryGetKey("SaveKey.v2", out byte[] newKey));
            CollectionAssert.AreNotEqual(oldKey, newKey);

            Assert.IsTrue(provider.DeleteKey("SaveKey"));
            Assert.IsFalse(provider.TryGetKey("SaveKey", out _));
            Assert.IsTrue(provider.TryGetKey("SaveKey.v2", out _));
            Array.Clear(oldKey, 0, oldKey.Length);
            Array.Clear(newKey, 0, newKey.Length);
        }

        [Test]
        public void ProviderRejectsContentKeyNamespace()
        {
            var provider = new InstallSaveKeyProvider(new MemoryKeyStore());

            Assert.Throws<RFrameworkException>(() => provider.EnsureKey("ContentKey"));
            Assert.IsFalse(provider.TryGetKey("ContentKey", out byte[] key));
            Assert.IsNull(key);
        }

        [Test]
        public void StorageComponentCanInstallManagedSaveKeyStore()
        {
            var store = new MemoryKeyStore();
            var owner = new GameObject("StorageComponentKeyTest");
            try
            {
                StorageComponent component = owner.AddComponent<StorageComponent>();
                component.SetManagedSaveKeyStore(store, "SaveKey.v2");

                Assert.NotNull(component.ManagedSaveKeyProvider);
                Assert.AreEqual(store.GetType().FullName, component.ManagedSaveKeyStoreTypeName);
                Assert.IsTrue(component.ManagedSaveKeyProvider.TryGetKey(
                    "SaveKey.v2", out byte[] key));
                Assert.AreEqual(InstallSaveKeyProvider.KeySize, key.Length);
                Assert.AreEqual(1, store.Count);
                Array.Clear(key, 0, key.Length);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                RFrameworkModuleHost.StopAll();
            }
        }

        [Test]
        public void ContentKeyRegistryRejectsInstallSaveProvider()
        {
            var store = new MemoryKeyStore();
            var saveProvider = new InstallSaveKeyProvider(store);

            Assert.Throws<RFrameworkException>(
                () => RuntimeKeyProviderRegistry.ConfigureContentKeys(saveProvider));
            Assert.IsFalse(RuntimeKeyProviderRegistry.TryGetContentKeys(out _));
        }

        [Test]
        public void ContentKeyRegistryReturnsConfiguredProvider()
        {
            var provider = new StaticKeyProvider("ContentKey", CreateKey(51));
            try
            {
                RuntimeKeyProviderRegistry.ConfigureContentKeys(provider);

                Assert.IsTrue(RuntimeKeyProviderRegistry.TryGetContentKeys(
                    out IKeyProvider registered));
                Assert.AreSame(provider, registered);
            }
            finally
            {
                RuntimeKeyProviderRegistry.ResetContentKeys();
            }
        }

        [Test]
        public void FileKeyStorePersistsWithOpaqueFileName()
        {
            string root = CreateTemporaryDirectory();
            byte[] expected = CreateKey(17);
            try
            {
                var first = new FileKeyStore(root);
                first.Write("SaveKey.private", expected);

                string[] files = Directory.GetFiles(root, "*.key");
                Assert.AreEqual(1, files.Length);
                StringAssert.DoesNotContain("SaveKey", Path.GetFileName(files[0]));

                var restarted = new FileKeyStore(root);
                Assert.IsTrue(restarted.TryRead("SaveKey.private", out byte[] actual));
                CollectionAssert.AreEqual(expected, actual);
                Array.Clear(actual, 0, actual.Length);
            }
            finally
            {
                Array.Clear(expected, 0, expected.Length);
                DeleteTemporaryDirectory(root);
            }
        }

        [Test]
        public void MacOsKeychainNativeSourceTargetsOnlyMacPlayer()
        {
            const string path =
                "Assets/UnityRFramework/Samples/Expansion.Security.macOS/Plugins/macOS/UnityRFrameworkMacKeychain.mm";
            PluginImporter importer = AssetImporter.GetAtPath(path) as PluginImporter;
            if (importer == null)
            {
                Assert.Ignore("未导入 Expansion.Security.macOS。");
            }

            Assert.IsFalse(importer.GetCompatibleWithAnyPlatform());
            Assert.IsFalse(importer.GetCompatibleWithEditor());
            Assert.IsTrue(importer.GetCompatibleWithPlatform(BuildTarget.StandaloneOSX));
            Assert.IsFalse(importer.GetCompatibleWithPlatform(BuildTarget.iOS));
            Assert.IsFalse(importer.GetCompatibleWithPlatform(BuildTarget.tvOS));
            CollectionAssert.AreEqual(
                new[] { "ENABLE_IL2CPP" }, importer.DefineConstraints);
        }

#if UNITY_EDITOR_WIN
        [Test]
        public void WindowsDpapiKeyStoreProtectsPersistedKey()
        {
            string root = CreateTemporaryDirectory();
            byte[] expected = CreateKey(33);
            try
            {
                Type type = FindType("UnityRFramework.Expansion.WindowsDpapiKeyStore");
                Assert.NotNull(type, "已导入的 Windows DPAPI 扩展类型不存在。");
                var first = (IKeyStore)Activator.CreateInstance(type, root);
                first.Write("SaveKey", expected);

                string[] files = Directory.GetFiles(root, "*.key");
                Assert.AreEqual(1, files.Length);
                CollectionAssert.AreNotEqual(expected, File.ReadAllBytes(files[0]));

                var restarted = (IKeyStore)Activator.CreateInstance(type, root);
                Assert.IsTrue(restarted.TryRead("SaveKey", out byte[] actual));
                CollectionAssert.AreEqual(expected, actual);
                Array.Clear(actual, 0, actual.Length);
            }
            finally
            {
                Array.Clear(expected, 0, expected.Length);
                DeleteTemporaryDirectory(root);
            }
        }
#endif

        private static byte[] CreateKey(byte seed)
        {
            byte[] key = new byte[InstallSaveKeyProvider.KeySize];
            for (int i = 0; i < key.Length; i++) key[i] = (byte)(seed + i);
            return key;
        }

        private static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "UnityRFrameworkKeyTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteTemporaryDirectory(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }

        private static Type FindType(string fullName)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        private sealed class MemoryKeyStore : IKeyStore
        {
            private readonly Dictionary<string, byte[]> values =
                new Dictionary<string, byte[]>(StringComparer.Ordinal);

            public int Count => values.Count;

            public bool TryRead(string keyId, out byte[] key)
            {
                if (!values.TryGetValue(keyId, out byte[] stored))
                {
                    key = null;
                    return false;
                }

                key = (byte[])stored.Clone();
                return true;
            }

            public void Write(string keyId, byte[] key)
            {
                values[keyId] = (byte[])key.Clone();
            }

            public bool Delete(string keyId)
            {
                return values.Remove(keyId);
            }
        }

        private sealed class StaticKeyProvider : IKeyProvider
        {
            private readonly string keyId;
            private readonly byte[] key;

            public StaticKeyProvider(string keyId, byte[] key)
            {
                this.keyId = keyId;
                this.key = key;
            }

            public bool TryGetKey(string requestedKeyId, out byte[] result)
            {
                if (!string.Equals(keyId, requestedKeyId, StringComparison.Ordinal))
                {
                    result = null;
                    return false;
                }

                result = (byte[])key.Clone();
                return true;
            }
        }
    }
}
