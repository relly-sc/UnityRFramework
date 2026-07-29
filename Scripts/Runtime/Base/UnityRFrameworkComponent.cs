
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 游戏框架组件抽象类。
    /// </summary>
    public abstract class UnityRFrameworkComponent : MonoBehaviour
    {
        /// <summary>
        /// 游戏框架组件初始化。
        /// </summary>
        protected virtual void Awake()
        {
            UnityRFrameworkComponentEntry.RegisterComponent(this);
        }
    }
}
