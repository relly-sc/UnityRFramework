using System;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// AOT 启动层传递给热更新入口的默认上下文实现。
    /// </summary>
    public sealed class HybridCLRHotUpdateContext : IHotUpdateContext
    {
        private readonly Func<CancellationToken, Task> continueAction;

        /// <summary>
        /// 创建热更新运行上下文。
        /// </summary>
        /// <param name="resource">已初始化的资源组件。</param>
        /// <param name="continueAction">进入正式业务流程的回调。</param>
        public HybridCLRHotUpdateContext(
            ResourceComponent resource,
            Func<CancellationToken, Task> continueAction)
        {
            Resource = resource != null
                ? resource
                : throw new RFrameworkException(
                    "HybridCLR hot update context resource is invalid.");
            this.continueAction = continueAction
                ?? throw new RFrameworkException(
                    "HybridCLR hot update continue action is invalid.");
        }

        /// <inheritdoc />
        public ResourceComponent Resource { get; }

        /// <inheritdoc />
        public string CodeVersion { get; private set; }

        /// <inheritdoc />
        public Task ContinueAsync(CancellationToken ct)
        {
            return continueAction(ct);
        }

        internal void SetCodeVersion(string codeVersion)
        {
            if (string.IsNullOrWhiteSpace(codeVersion))
            {
                throw new RFrameworkException(
                    "HybridCLR hot update code version is empty.");
            }

            CodeVersion = codeVersion;
        }
    }
}
