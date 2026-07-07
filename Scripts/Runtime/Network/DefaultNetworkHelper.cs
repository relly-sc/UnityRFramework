using RFramework.Network;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认网络辅助器（空实现）。网络传输层必须由项目自行实现。
    /// 请在 Expansion 层创建真实的 TcpHelper / WebSocketHelper 并替换。
    /// </summary>
    public class DefaultNetworkHelper : NetworkHelperBase
    {
        /// <inheritdoc/>
        public override void Connect(string ip, int port)
        {
            Log.Error("DefaultNetworkHelper: Connect is not implemented. Provide a real INetworkHelper implementation.");
        }

        /// <inheritdoc/>
        public override void Disconnect()
        {
        }

        /// <inheritdoc/>
        public override void Send(byte[] data)
        {
            Log.Error("DefaultNetworkHelper: Send is not implemented.");
        }

        /// <inheritdoc/>
        public override void Update()
        {
        }

        /// <inheritdoc/>
        public override bool IsConnected
        {
            get { return false; }
        }
    }
}
