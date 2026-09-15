using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// NewtonsoftJsonHelper 的序列化与反序列化测试。
    /// </summary>
    public sealed class NewtonsoftJsonHelperTests
    {
        /// <summary>
        /// 验证属性、字典和顶层集合可以完整往返。
        /// </summary>
        [Test]
        public void RoundTripPreservesPropertiesDictionaryAndTopLevelCollection()
        {
            NewtonsoftJsonHelper helper = new NewtonsoftJsonHelper();
            List<TestPayload> source = new List<TestPayload>
            {
                new TestPayload
                {
                    Id = 7,
                    Name = "测试",
                    Attributes = new Dictionary<string, decimal>
                    {
                        { "Power", 12.5m }
                    }
                }
            };

            string json = helper.ToJson(source);
            List<TestPayload> result = helper.ToObject<List<TestPayload>>(json);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(7, result[0].Id);
            Assert.AreEqual("测试", result[0].Name);
            Assert.AreEqual(12.5m, result[0].Attributes["Power"]);
        }

        /// <summary>
        /// 验证运行时 Type 重载返回正确的目标类型。
        /// </summary>
        [Test]
        public void RuntimeTypeOverloadCreatesRequestedType()
        {
            NewtonsoftJsonHelper helper = new NewtonsoftJsonHelper();

            object result = helper.ToObject(
                typeof(TestPayload),
                "{\"Id\":9,\"Name\":\"Runtime\",\"Attributes\":{\"Power\":3.25}}");

            Assert.IsInstanceOf<TestPayload>(result);
            TestPayload payload = (TestPayload)result;
            Assert.AreEqual(9, payload.Id);
            Assert.AreEqual("Runtime", payload.Name);
            Assert.AreEqual(3.25m, payload.Attributes["Power"]);
        }

        /// <summary>
        /// 测试使用的属性与字典数据对象。
        /// </summary>
        [Serializable]
        private sealed class TestPayload
        {
            /// <summary>
            /// 数据编号。
            /// </summary>
            public int Id { get; set; }

            /// <summary>
            /// 数据名称。
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// 属性集合。
            /// </summary>
            public Dictionary<string, decimal> Attributes { get; set; }
        }
    }
}
