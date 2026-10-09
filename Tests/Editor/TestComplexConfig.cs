using System;
using System.Collections.Generic;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>复杂字段闭环测试使用的状态枚举。</summary>
    internal enum TestComplexConfigStateEnum
    {
        /// <summary>空闲。</summary>
        Idle = 0,
        /// <summary>运行。</summary>
        Run = 2
    }

    /// <summary>复杂字段闭环测试使用的配置行。</summary>
    [Serializable]
    internal sealed class TestComplexConfig
    {
        public int Id;
        public TestComplexConfigStateEnum State;
        public int[] Levels;
        public List<string> Tags;
        public string Description;
    }
}
