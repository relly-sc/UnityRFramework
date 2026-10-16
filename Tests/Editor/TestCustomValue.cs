using System;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// ConfigPipeline 自定义字段 Codec 验收使用的二维整数值。
    /// </summary>
    [Serializable]
    public struct TestCustomValue : IEquatable<TestCustomValue>
    {
        /// <summary>横向值。</summary>
        public int X;

        /// <summary>纵向值。</summary>
        public int Y;

        /// <inheritdoc/>
        public bool Equals(TestCustomValue other)
        {
            return X == other.X && Y == other.Y;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is TestCustomValue other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }
    }
}
