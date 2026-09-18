using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RFramework;
using UnityEngine;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>验证可选 YooAsset Helper 的平台文件系统选择，不运行下载或构建。</summary>
    public sealed class YooAssetPlatformOptionsTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetProtection();
        }

        [TearDown]
        public void TearDown()
        {
            ResetProtection();
        }

        [TestCase(ResourcePlayMode.Host, RuntimePlatform.WebGLPlayer,
            "WebPlayModeOptions", "WebNetworkFileSystemParameters", "YooAsset.WebNetworkFileSystem")]
        [TestCase(ResourcePlayMode.Offline, RuntimePlatform.WebGLPlayer,
            "WebPlayModeOptions", "WebServerFileSystemParameters", "YooAsset.WebServerFileSystem")]
        [TestCase(ResourcePlayMode.Host, RuntimePlatform.WindowsPlayer,
            "HostPlayModeOptions", "CacheFileSystemParameters", "YooAsset.SandboxFileSystem")]
        [TestCase(ResourcePlayMode.Offline, RuntimePlatform.IPhonePlayer,
            "OfflinePlayModeOptions", "BuiltinFileSystemParameters", "YooAsset.BuiltinFileSystem")]
        public void InitializationUsesSupportedFileSystem(ResourcePlayMode mode,
            RuntimePlatform platform, string optionName, string propertyName, string fileSystemName)
        {
            Type helper = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("UnityRFramework.Expansion.YooAssetResourceHelper"))
                .FirstOrDefault(type => type != null);
            if (helper == null)
            {
                Assert.Ignore("未导入 Expansion.YooAsset。");
            }

            MethodInfo factory = helper.GetMethod("CreateInitializeOptions",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(factory, Is.Not.Null);
            object options = factory.Invoke(null, new object[]
            {
                "TestPackage", mode, "https://example.com/bundles",
                "https://example.com/backup", null, platform
            });
            Assert.That(options.GetType().Name, Is.EqualTo(optionName));
            object parameters = options.GetType().GetProperty(propertyName).GetValue(options);
            Assert.That(parameters, Is.Not.Null);
            Assert.That(parameters.GetType().GetProperty("FileSystemTypeName").GetValue(parameters),
                Is.EqualTo(fileSystemName));
            if (platform == RuntimePlatform.WebGLPlayer)
            {
                string other = mode == ResourcePlayMode.Host
                    ? "WebServerFileSystemParameters" : "WebNetworkFileSystemParameters";
                Assert.That(options.GetType().GetProperty(other).GetValue(options), Is.Null);
            }
        }

        [Test]
        public void ConfiguredProtectionAddsDecryptorsToRuntimeFileSystems()
        {
            Type protection = FindType(
                "UnityRFramework.Expansion.YooAssetBundleProtection");
            Type helper = FindType(
                "UnityRFramework.Expansion.YooAssetResourceHelper");
            if (protection == null || helper == null)
            {
                Assert.Ignore("未导入 Expansion.YooAsset。");
            }

            RuntimeKeyProviderRegistry.ConfigureContentKeys(new TestKeyProvider());

            MethodInfo factory = helper.GetMethod(
                "CreateInitializeOptions",
                BindingFlags.NonPublic | BindingFlags.Static);
            object options = factory.Invoke(null, new object[]
            {
                "TestPackage",
                ResourcePlayMode.Host,
                "https://example.com/bundles",
                "https://example.com/backup",
                null,
                RuntimePlatform.WindowsPlayer
            });

            AssertFileSystemHasDecryptors(
                options.GetType().GetProperty("BuiltinFileSystemParameters")
                    .GetValue(options));
            AssertFileSystemHasDecryptors(
                options.GetType().GetProperty("CacheFileSystemParameters")
                    .GetValue(options));
        }

        [Test]
        public void BundleProtectionRoundTripRejectsTampering()
        {
            Type protection = FindType(
                "UnityRFramework.Expansion.YooAssetBundleProtection");
            if (protection == null)
            {
                Assert.Ignore("未导入 Expansion.YooAsset。");
            }

            TestKeyProvider provider = new TestKeyProvider();
            protection.GetMethod("Configure", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { provider });
            byte[] source = { 1, 2, 3, 4, 5 };
            byte[] protectedData = (byte[])protection.GetMethod(
                    "Protect",
                    BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { source, "YooAssetBundle", provider });
            MethodInfo unprotect = protection.GetMethod(
                "Unprotect",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(
                (byte[])unprotect.Invoke(null, new object[] { protectedData }),
                Is.EqualTo(source));

            protectedData[protectedData.Length - 1] ^= 1;
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => unprotect.Invoke(null, new object[] { protectedData }));
            Assert.That(exception.InnerException, Is.TypeOf<RFrameworkException>());
        }

        [Test]
        public void BuilderEncryptorRequiresValidEnvironmentKey()
        {
            Type encryptor = FindType(
                "UnityRFramework.Expansion.Editor.UnityRFrameworkBundleEncryptor");
            Type protection = FindType(
                "UnityRFramework.Expansion.YooAssetBundleProtection");
            if (encryptor == null || protection == null)
            {
                Assert.Ignore("未导入 Expansion.YooAsset。");
            }

            string keyVariable = (string)protection.GetField(
                    "BuildKeyEnvironmentVariable",
                    BindingFlags.Public | BindingFlags.Static)
                .GetRawConstantValue();
            string keyIdVariable = (string)protection.GetField(
                    "BuildKeyIdEnvironmentVariable",
                    BindingFlags.Public | BindingFlags.Static)
                .GetRawConstantValue();
            string previousKey = Environment.GetEnvironmentVariable(keyVariable);
            string previousKeyId = Environment.GetEnvironmentVariable(keyIdVariable);
            MethodInfo validate = encryptor.GetMethod(
                "TryValidateEnvironment",
                BindingFlags.NonPublic | BindingFlags.Static);

            try
            {
                Environment.SetEnvironmentVariable(keyVariable, null);
                object[] missingArguments = { null };
                Assert.That(
                    (bool)validate.Invoke(null, missingArguments),
                    Is.False);
                Assert.That((string)missingArguments[0], Does.Contain(keyVariable));

                Environment.SetEnvironmentVariable(
                    keyVariable,
                    Convert.ToBase64String(new byte[32]));
                Environment.SetEnvironmentVariable(keyIdVariable, "TestBundleKey");
                object[] validArguments = { null };
                Assert.That(
                    (bool)validate.Invoke(null, validArguments),
                    Is.True);
                Assert.That(validArguments[0], Is.Null);
            }
            finally
            {
                Environment.SetEnvironmentVariable(keyVariable, previousKey);
                Environment.SetEnvironmentVariable(keyIdVariable, previousKeyId);
            }
        }

        [Test]
        public void ConfigProtectionCanBeNestedInsideBundleProtection()
        {
            Type protection = FindType(
                "UnityRFramework.Expansion.YooAssetBundleProtection");
            if (protection == null)
            {
                Assert.Ignore("未导入 Expansion.YooAsset。");
            }

            LayeredTestKeyProvider provider = new LayeredTestKeyProvider();
            DefaultDataProtector dataProtector = new DefaultDataProtector(provider);
            ConfigProtectionContext configContext = new ConfigProtectionContext(
                ConfigProtectionMode.EncryptedAndAuthenticated,
                "Config/Game.bytes",
                ConfigPayloadType.Single,
                ConfigPayloadFormat.BinarySingleTable);
            byte[] configPlaintext = { 10, 20, 30, 40 };
            byte[] protectedConfig = configContext.Protect(
                dataProtector,
                configPlaintext,
                "ConfigContentKey");

            byte[] protectedBundle = (byte[])protection.GetMethod(
                    "Protect",
                    BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[]
                {
                    protectedConfig,
                    "YooAssetBundleKey",
                    provider
                });
            protection.GetMethod("Configure", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { provider });
            byte[] restoredConfig = (byte[])protection.GetMethod(
                    "Unprotect",
                    BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { protectedBundle });
            byte[] restoredPlaintext = configContext.Unprotect(
                dataProtector,
                restoredConfig);

            Assert.That(restoredConfig, Is.EqualTo(protectedConfig));
            Assert.That(restoredPlaintext, Is.EqualTo(configPlaintext));
        }

        private static void AssertFileSystemHasDecryptors(object parameters)
        {
            FieldInfo field = parameters.GetType().GetField(
                "_createParameters",
                BindingFlags.Instance | BindingFlags.NonPublic);
            IDictionary values = (IDictionary)field.GetValue(parameters);
            string[] keys =
            {
                "AssetBundleDecryptor",
                "AssetBundleFallbackDecryptor",
                "RawBundleDecryptor",
                "ArchiveBundleDecryptor"
            };
            foreach (string key in keys)
            {
                Assert.That(values.Contains(key), Is.True, key);
                Assert.That(
                    values[key].GetType().FullName,
                    Is.EqualTo(
                        "UnityRFramework.Expansion.UnityRFrameworkBundleDecryptor"));
            }
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(type => type != null);
        }

        private static void ResetProtection()
        {
            RuntimeKeyProviderRegistry.ResetContentKeys();
            FindType("UnityRFramework.Expansion.YooAssetBundleProtection")
                ?.GetMethod("Reset", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, null);
        }

        private sealed class TestKeyProvider : IKeyProvider
        {
            private static readonly byte[] Key =
            {
                0, 1, 2, 3, 4, 5, 6, 7,
                8, 9, 10, 11, 12, 13, 14, 15,
                16, 17, 18, 19, 20, 21, 22, 23,
                24, 25, 26, 27, 28, 29, 30, 31
            };

            public bool TryGetKey(string keyId, out byte[] key)
            {
                if (!string.Equals(
                        keyId,
                        "YooAssetBundle",
                        StringComparison.Ordinal))
                {
                    key = null;
                    return false;
                }

                key = (byte[])Key.Clone();
                return true;
            }
        }

        private sealed class LayeredTestKeyProvider : IKeyProvider
        {
            public bool TryGetKey(string keyId, out byte[] key)
            {
                byte seed;
                if (string.Equals(
                        keyId,
                        "ConfigContentKey",
                        StringComparison.Ordinal))
                {
                    seed = 17;
                }
                else if (string.Equals(
                             keyId,
                             "YooAssetBundleKey",
                             StringComparison.Ordinal))
                {
                    seed = 83;
                }
                else
                {
                    key = null;
                    return false;
                }

                key = new byte[32];
                for (int i = 0; i < key.Length; i++)
                {
                    key[i] = (byte)(seed + i);
                }

                return true;
            }
        }
    }
}
