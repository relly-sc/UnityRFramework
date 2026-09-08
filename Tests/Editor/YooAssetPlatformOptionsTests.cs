using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>验证可选 YooAsset Helper 的平台文件系统选择，不运行下载或构建。</summary>
    public sealed class YooAssetPlatformOptionsTests
    {
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
    }
}
