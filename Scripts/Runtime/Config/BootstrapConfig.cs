namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 启动配置数据类，供 YooAsset 等资源系统在启动阶段从 StreamingAssets 加载。
    /// 与 Luban 生成的配置表无关——这是一个独立的轻量配置。
    /// </summary>
    public class BootstrapConfig
    {
        /// <summary>
        /// 远程资源服务器地址。
        /// </summary>
        public string RemoteUrl = "";

        /// <summary>
        /// 渠道标识。
        /// </summary>
        public string Channel = "";

        /// <summary>
        /// 最低客户端版本号（低于此版本需强制更新）。
        /// </summary>
        public string MinimalVersion = "";

        /// <summary>
        /// 是否为调试模式。
        /// </summary>
        public bool DebugMode = false;

        /// <summary>
        /// 资源运行模式：0=EditorSimulate, 1=Offline, 2=Host。
        /// </summary>
        public int PlayMode = 0;
    }
}
