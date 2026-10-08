using System.Threading;
using System.Threading.Tasks;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 定义 AOT 启动层调用热更新业务的稳定入口。
    /// </summary>
    public interface IHotUpdateEntry
    {
        /// <summary>
        /// 启动当前热更新业务入口。
        /// </summary>
        /// <param name="context">由 AOT 启动层提供的运行上下文。</param>
        /// <param name="ct">取消令牌。</param>
        Task StartAsync(IHotUpdateContext context, CancellationToken ct);

        /// <summary>
        /// 同步停止入口并释放由热更新业务持有的对象和资源引用。
        /// </summary>
        void Shutdown();
    }
}
