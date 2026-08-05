using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 可由 <see cref="GameEntry"/> 查询的框架组件基类。
    /// </summary>
    public abstract class UnityRFrameworkComponent : MonoBehaviour
    {
        /// <summary>
        /// 注册当前组件。
        /// </summary>
        protected virtual void Awake()
        {
            UnityRFrameworkRuntime.Register(this);
        }
    }
}
