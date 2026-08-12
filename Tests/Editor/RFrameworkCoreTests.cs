using System.Collections.Generic;
using NUnit.Framework;
using RFramework;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 模块调度与日志桥接的最小核心契约测试。
    /// </summary>
    public sealed class RFrameworkCoreTests
    {
        [TearDown]
        public void TearDown()
        {
            RFrameworkLog.Clear();
            try
            {
                RFrameworkModuleHost.StopAll();
            }
            catch (RFrameworkException)
            {
                // 测试清理继续执行，具体停止错误由对应测试断言。
            }
        }

        /// <summary>验证模块可缓存、统一停止，并在停止后重新创建。</summary>
        [Test]
        public void RFrameworkModuleHostCanStopAndRecreateModules()
        {
            IPoolModule first = RFrameworkModuleHost.Get<IPoolModule>();

            Assert.AreSame(first, RFrameworkModuleHost.Get<IPoolModule>());
            Assert.AreEqual(1, RFrameworkModuleHost.Count);

            RFrameworkModuleHost.Tick(0.016f, 0.016f);
            RFrameworkModuleHost.StopAll();

            Assert.AreEqual(0, RFrameworkModuleHost.Count);
            Assert.AreNotSame(first, RFrameworkModuleHost.Get<IPoolModule>());
        }

        /// <summary>验证强制日志与关闭后安全日志具有不同失败语义。</summary>
        [Test]
        public void LogHelperSupportsRequiredAndSafeWrites()
        {
            RecordingLogHelper helper = new RecordingLogHelper();
            RFrameworkLog.SetHelper(helper);

            RFrameworkLog.Write(LogLevel.Info, "Value {0}", 7);
            Assert.AreEqual("Value 7", helper.Messages[0]);

            RFrameworkLog.Clear();
            Assert.IsTrue(helper.IsDisposed);
            Assert.IsFalse(RFrameworkLog.TryWrite(LogLevel.Warning, "late"));
            Assert.Throws<RFrameworkException>(() =>
                RFrameworkLog.Write(LogLevel.Error, "required"));
        }

        private sealed class RecordingLogHelper : ILogHelper
        {
            public readonly List<string> Messages = new List<string>();

            public bool IsDisposed { get; private set; }

            public void Write(LogLevel level, string message)
            {
                Messages.Add(message);
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
