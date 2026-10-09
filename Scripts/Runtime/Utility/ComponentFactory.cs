using System;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 根据类型名或 Inspector 引用创建模块 Helper 组件。
    /// </summary>
    public static class ComponentFactory
    {
        /// <summary>
        /// 创建指定 Helper。场景内自定义实例在 index 为 0 时直接复用，其余情况复制。
        /// </summary>
        /// <typeparam name="T">Helper 基类。</typeparam>
        /// <param name="typeName">待创建的具体类型名；为空时使用 customHelper。</param>
        /// <param name="customHelper">Inspector 提供的自定义 Helper。</param>
        /// <param name="index">同类 Helper 的索引。</param>
        /// <returns>创建或复用的 Helper；配置无效时返回 null。</returns>
        public static T Create<T>(string typeName, T customHelper, int index = 0)
            where T : MonoBehaviour
        {
            if (!string.IsNullOrWhiteSpace(typeName))
            {
                Type concreteType = Utility.Assembly.GetType(typeName);
                if (concreteType == null || !typeof(T).IsAssignableFrom(concreteType))
                {
                    Log.Warning(
                        "Helper type '{0}' was not found or does not derive from '{1}'.",
                        typeName, typeof(T).FullName);
                    return null;
                }

                GameObject owner = new GameObject(concreteType.Name);
                return (T)owner.AddComponent(concreteType);
            }

            if (customHelper == null)
            {
                Log.Warning("No custom helper was assigned for '{0}'.", typeof(T).FullName);
                return null;
            }

            return customHelper.gameObject.InScene() && index == 0
                ? customHelper
                : UnityEngine.Object.Instantiate(customHelper);
        }
    }
}
