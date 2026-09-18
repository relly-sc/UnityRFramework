using System;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// URFC v2 闭环测试使用的配置行。
    /// </summary>
    [Serializable]
    internal sealed class TestConfigRow
    {
        /// <summary>测试配置 Id。</summary>
        public int Id;

        /// <summary>测试配置名称。</summary>
        public string Name;

        /// <summary>测试配置价格。</summary>
        public float Price;
    }
}
