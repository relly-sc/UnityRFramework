using System;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 自定义字段 Codec 闭环测试使用的配置行。
    /// </summary>
    [Serializable]
    [ConfigTable("TestCustom")]
    internal sealed class TestCustomConfig
    {
        /// <summary>测试配置 Id。</summary>
        public int Id;

        /// <summary>测试二维整数值。</summary>
        public TestCustomValue Point;
    }
}
