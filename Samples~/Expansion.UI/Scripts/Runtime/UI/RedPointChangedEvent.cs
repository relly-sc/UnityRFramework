namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 红点节点聚合值变化事件。
    /// </summary>
    public readonly struct RedPointChangedEvent
    {
        /// <summary>初始化红点变化事件。</summary>
        /// <param name="path">节点完整路径。</param>
        /// <param name="value">节点最新聚合值。</param>
        public RedPointChangedEvent(string path, int value)
        {
            Path = path;
            Value = value;
        }

        /// <summary>获取节点完整路径。</summary>
        public string Path { get; }

        /// <summary>获取节点最新聚合值。</summary>
        public int Value { get; }
    }
}
