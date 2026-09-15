using System.Threading;
using System.Threading.Tasks;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 定义 AOT 启动层提供给热更新入口的最小运行上下文。
    /// </summary>
    public interface IHotUpdateContext
    {
        /// <summary>获取框架资源组件。</summary>
        ResourceComponent Resource { get; }

        /// <summary>获取当前 Manifest 声明的代码版本。</summary>
        string CodeVersion { get; }

        /// <summary>
        /// 请求 AOT 启动层继续进入正式业务流程。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        Task ContinueAsync(CancellationToken ct);
    }
}
