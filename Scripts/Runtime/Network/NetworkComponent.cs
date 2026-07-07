using System.Threading;
using System.Threading.Tasks;
using RFramework;
using RFramework.Event;
using RFramework.Network;
using RFramework.Timer;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 网络组件。负责 Inspector 配置 + 纯转发到 NetworkModule。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Network")]
    [DisallowMultipleComponent]
    public sealed class NetworkComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 网络辅助器类型全名。
        /// </summary>
        [SerializeField]
        [Tooltip("网络辅助器类型全名。必须是继承自 NetworkHelperBase 的 MonoBehaviour。")]
        private string networkHelperTypeName = "UnityRFramework.Expansion.Network.TcpNetworkHelper";

        /// <summary>
        /// 心跳间隔（秒）。0 表示不发送心跳。
        /// </summary>
        [SerializeField]
        [Tooltip("心跳间隔（秒）。0 表示不发送心跳。")]
        private float heartbeatInterval = 5f;

        /// <summary>
        /// 是否启用自动重连。
        /// </summary>
        [SerializeField]
        [Tooltip("是否启用自动重连。")]
        private bool autoReconnect = true;

        /// <summary>
        /// 重连间隔（秒）。
        /// </summary>
        [SerializeField]
        [Tooltip("重连间隔（秒）。")]
        private float reconnectInterval = 3f;

        /// <summary>
        /// 网络模块引用。
        /// </summary>
        private INetworkModule networkModule;

        /// <summary>
        /// 是否已连接。
        /// </summary>
        public bool IsConnected
        {
            get { return networkModule != null && networkModule.IsConnected; }
        }

        /// <inheritdoc/>
        protected override void Awake()
        {
            base.Awake();

            networkModule = RFrameworkModuleEntry.GetModule<INetworkModule>();
            if (networkModule == null)
            {
                Log.Error("Can not find module '{0}'.", nameof(INetworkModule));
                return;
            }

            IEventModule eventModule = RFrameworkModuleEntry.GetModule<IEventModule>();
            ITimerModule timerModule = RFrameworkModuleEntry.GetModule<ITimerModule>();
            networkModule.SetDependencies(eventModule, timerModule);

            networkModule.HeartbeatInterval = heartbeatInterval;
            networkModule.AutoReconnect = autoReconnect;
            networkModule.ReconnectInterval = reconnectInterval;

            NetworkHelperBase helper = Helper.CreateHelper<NetworkHelperBase>(networkHelperTypeName, null);
            if (helper != null)
            {
                networkModule.SetHelper(helper);
                helper.transform.SetParent(transform);
            }
            else
            {
                Log.Error(
                    "NetworkComponent: Helper type '{0}' is null. Provide a real INetworkHelper in Expansion layer.",
                    networkHelperTypeName);
            }
        }

        /// <summary>
        /// 设置网络辅助器（运行时替换）。
        /// </summary>
        /// <param name="helper">网络辅助器实例。</param>
        public void SetHelper(INetworkHelper helper)
        {
            if (helper == null)
            {
                throw new RFrameworkException("NetworkComponent: helper is invalid.");
            }

            networkModule.SetHelper(helper);
        }

        /// <inheritdoc cref="INetworkModule.ConnectAsync"/>
        public Task ConnectAsync(string ip, int port, CancellationToken ct = default)
        {
            return networkModule.ConnectAsync(ip, port, ct);
        }

        /// <inheritdoc cref="INetworkModule.Disconnect"/>
        public void Disconnect()
        {
            networkModule.Disconnect();
        }

        /// <inheritdoc cref="INetworkModule.Send"/>
        public void Send(int msgId, byte[] body)
        {
            networkModule.Send(msgId, body);
        }

        /// <inheritdoc cref="INetworkModule.RegisterHandler"/>
        public void RegisterHandler(int msgId, System.Action<byte[]> handler)
        {
            networkModule.RegisterHandler(msgId, handler);
        }

        /// <inheritdoc cref="INetworkModule.UnregisterHandler"/>
        public void UnregisterHandler(int msgId)
        {
            networkModule.UnregisterHandler(msgId);
        }
    }
}
