using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 网络辅助器基类。
    /// 具体实现（TCP/WebSocket/KCP）放在 Expansion 层。
    /// </summary>
    public abstract class NetworkHelperBase : MonoBehaviour, INetworkHelper
    {
        /// <inheritdoc/>
        public abstract void Connect(string ip, int port);

        /// <inheritdoc/>
        public abstract void Disconnect();

        /// <inheritdoc/>
        public abstract void Send(int msgId, byte[] body);

        /// <inheritdoc/>
        public abstract void Update();

        /// <inheritdoc/>
        public abstract bool IsConnected { get; }

        /// <inheritdoc/>
        public System.Action<int, byte[]> OnReceive { get; set; }

        /// <inheritdoc/>
        public System.Action OnConnected { get; set; }

        /// <inheritdoc/>
        public System.Action OnDisconnected { get; set; }

        /// <inheritdoc/>
        public System.Action<string> OnError { get; set; }
    }
}
