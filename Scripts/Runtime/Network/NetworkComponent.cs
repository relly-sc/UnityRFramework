using System.Collections.Generic;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 网络组件。负责 Inspector 配置 + 纯转发到 NetworkModule。
    /// 支持多通道管理：登录服、聊天服、游戏服等可同时在线。
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
        private string networkHelperTypeName = "UnityRFramework.Runtime.TcpNetworkHelper";

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

        // ====== 内置属性 ======

        /// <summary>
        /// 获取默认通道。未创建任何通道时返回 null。
        /// </summary>
        public INetworkChannel DefaultChannel
        {
            get { return networkModule?.DefaultChannel; }
        }

        /// <summary>
        /// 获取当前网络通道数量。
        /// </summary>
        public int ChannelCount
        {
            get { return networkModule != null ? networkModule.ChannelCount : 0; }
        }

        /// <inheritdoc/>
        protected override void Awake()
        {
            base.Awake();

            networkModule = RFrameworkModuleHost.Get<INetworkModule>();
            if (networkModule == null)
            {
                Log.Error("Can not find module '{0}'.", nameof(INetworkModule));
                return;
            }

            IEventModule eventModule = RFrameworkModuleHost.Get<IEventModule>();
            ITimerModule timerModule = RFrameworkModuleHost.Get<ITimerModule>();
            networkModule.SetDependencies(eventModule, timerModule);

            // 创建默认通道并配置
            INetworkChannel defaultChannel = networkModule.CreateChannel("Default");
            defaultChannel.HeartbeatInterval = heartbeatInterval;
            defaultChannel.AutoReconnect = autoReconnect;
            defaultChannel.ReconnectInterval = reconnectInterval;

            NetworkHelperBase helper = ComponentFactory.Create<NetworkHelperBase>(networkHelperTypeName, null);
            if (helper != null)
            {
                defaultChannel.SetHelper(helper);
                helper.transform.SetParent(transform);
            }
            else
            {
                Log.Error(
                    "NetworkComponent: Helper type '{0}' is null. Provide a real INetworkHelper implementation.",
                    networkHelperTypeName);
            }
        }

        // ====== 多通道管理 ======

        /// <summary>
        /// 创建网络通道。同名通道会返回已有实例。
        /// </summary>
        /// <param name="name">通道名称（如 "Login"、"Chat"），全局唯一。</param>
        /// <returns>通道实例。</returns>
        public INetworkChannel CreateChannel(string name)
        {
            return networkModule.CreateChannel(name);
        }

        /// <summary>
        /// 获取已存在的通道。
        /// </summary>
        /// <param name="name">通道名称。</param>
        /// <returns>通道实例，不存在时返回 null。</returns>
        public INetworkChannel GetChannel(string name)
        {
            return networkModule.GetChannel(name);
        }

        /// <summary>
        /// 是否存在指定名称的通道。
        /// </summary>
        /// <param name="name">通道名称。</param>
        public bool HasChannel(string name)
        {
            return networkModule.HasChannel(name);
        }

        /// <summary>
        /// 移除并关闭通道。
        /// </summary>
        /// <param name="name">通道名称。</param>
        /// <returns>是否找到并移除成功。</returns>
        public bool RemoveChannel(string name)
        {
            return networkModule.RemoveChannel(name);
        }

        /// <summary>
        /// 获取所有通道列表（只读）。
        /// </summary>
        public IReadOnlyList<INetworkChannel> GetAllChannels()
        {
            return networkModule.GetAllChannels();
        }

    }
}
