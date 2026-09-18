using System;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>JSON 基础类型兼容性测试使用的配置行。</summary>
    [Serializable]
    internal sealed class TestJsonPrimitiveConfig
    {
        public int Id;
        public decimal DecimalValue;
        public char CharValue;
        public ulong ULongValue;
    }
}
