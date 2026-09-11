using System;
using System.Reflection;
using NUnit.Framework;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// UTF-8 C# 脚本模板生成契约测试。
    /// </summary>
    public sealed class Utf8ScriptTemplateBuilderTests
    {
        /// <summary>
        /// 普通 Class 默认不应继承 MonoBehaviour。
        /// </summary>
        [Test]
        public void ClassDoesNotInheritMonoBehaviourByDefault()
        {
            string script = Build(
                "PlainClass",
                "Game.Runtime",
                string.Empty);

            StringAssert.DoesNotContain("using UnityEngine;", script);
            StringAssert.Contains("public class PlainClass\n", script);
        }

        /// <summary>
        /// 勾选选项后 Class 应导入 UnityEngine 并继承 MonoBehaviour。
        /// </summary>
        [Test]
        public void ClassCanInheritMonoBehaviour()
        {
            string script = Build(
                "ViewController",
                "Game.Runtime",
                string.Empty,
                true);

            StringAssert.StartsWith("using UnityEngine;\n\n", script);
            StringAssert.Contains(
                "public class ViewController : MonoBehaviour",
                script);
        }

        /// <summary>
        /// 未勾选 MonoBehaviour 时仍应支持自定义父类。
        /// </summary>
        [Test]
        public void ClassCanUseCustomBaseType()
        {
            string script = Build(
                "DerivedType",
                string.Empty,
                "BaseType");

            StringAssert.DoesNotContain("using UnityEngine;", script);
            StringAssert.Contains(
                "public class DerivedType : BaseType",
                script);
        }

        private static string Build(
            string typeName,
            string namespaceName,
            string baseTypeName,
            bool inheritMonoBehaviour = false)
        {
            Type builderType = FindType(
                "UnityRFramework.Editor.Utf8ScriptTemplateBuilder");
            Type kindType = FindType("UnityRFramework.Editor.ScriptKind");
            object classKind = Enum.Parse(kindType, "Class");
            MethodInfo buildMethod = builderType.GetMethod(
                "Build",
                BindingFlags.Public | BindingFlags.Static);

            Assert.IsNotNull(buildMethod);
            return (string)buildMethod.Invoke(
                null,
                new[]
                {
                    typeName,
                    classKind,
                    namespaceName,
                    baseTypeName,
                    inheritMonoBehaviour
                });
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail("未找到 Editor 工具类型：" + fullName);
            return null;
        }
    }
}
