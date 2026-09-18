using System;
using System.Reflection;
using NUnit.Framework;
using RFramework;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>Protected 基础数值类型的最小行为测试。</summary>
    public sealed class ProtectedValueTests
    {
        [Test]
        public void ProtectedValuesRoundTripWithoutStoringPlainInt()
        {
            ProtectedInt value = new ProtectedInt(123456789);

            Assert.AreEqual(123456789, (int)value);
            Assert.AreNotEqual(123456789, ReadPrivate<int>(value, "maskedValue"));
        }

        [Test]
        public void ProtectedIntegerTypesRoundTrip()
        {
            Assert.AreEqual(-1234567890123L, (long)new ProtectedLong(-1234567890123L));
            Assert.AreEqual(12.5f, (float)new ProtectedFloat(12.5f));
            Assert.IsTrue((bool)new ProtectedBool(true));
            Assert.IsFalse((bool)new ProtectedBool(false));
        }

        [Test]
        public void ProtectedFloatPreservesSpecialBitPatterns()
        {
            float[] values = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0f };
            for (int i = 0; i < values.Length; i++)
            {
                float actual = (float)new ProtectedFloat(values[i]);
                Assert.AreEqual(FloatBits(values[i]), FloatBits(actual));
            }
        }

        [Test]
        public void TamperingRaisesMemoryTamperEventAndReturnsDefault()
        {
            IEventModule events = RFrameworkModuleHost.Get<IEventModule>();
            int eventCount = 0;
            Action<MemoryTamperEvent> handler = message =>
            {
                eventCount++;
                Assert.AreEqual(typeof(ProtectedInt).FullName, message.ValueTypeName);
            };

            events.Subscribe(handler);
            try
            {
                ProtectedInt value = new ProtectedInt(42);
                object boxed = value;
                FieldInfo field = typeof(ProtectedInt).GetField(
                    "maskedValue", BindingFlags.Instance | BindingFlags.NonPublic);
                field.SetValue(boxed, ReadPrivate<int>(value, "maskedValue") ^ 1);

                Assert.AreEqual(0, (int)(ProtectedInt)boxed);
                Assert.AreEqual(1, eventCount);
            }
            finally
            {
                events.Unsubscribe(handler);
            }
        }

        private static T ReadPrivate<T>(object value, string fieldName)
        {
            FieldInfo field = value.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, "缺少受保护字段：" + fieldName);
            return (T)field.GetValue(value);
        }

        private static int FloatBits(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        }
    }
}
