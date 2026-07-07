using RFramework.Entity;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 实体逻辑基类，用户继承此类编写实体行为。
    /// 生命周期方法使用 protected internal virtual：
    /// - protected：子类可重写
    /// - internal：Entity（同程序集）可调用
    /// - 外部不可直接调用
    /// </summary>
    /// <remarks>
    /// 设计约束：EntityLogic 是用户扩展点，不是 God Class。
    /// 如果逻辑过多，应拆分为多个独立组件。
    /// </remarks>
    public class EntityLogic : MonoBehaviour
    {
        /// <summary>
        /// 所属的 Entity 包装器引用。
        /// </summary>
        public Entity Owner { get; private set; }

        protected internal virtual void OnInit(Entity owner, bool isNewInstance, object userData)
        {
            Owner = owner;
        }

        protected internal virtual void OnRecycle()
        {
            Owner = null;
        }

        protected internal virtual void OnShow(object userData)
        {
        }

        protected internal virtual void OnHide(bool isShutdown, object userData)
        {
        }

        protected internal virtual void OnAttached(IEntity childEntity, object userData)
        {
        }

        protected internal virtual void OnDetached(IEntity childEntity, object userData)
        {
        }

        protected internal virtual void OnAttachTo(IEntity parentEntity, object userData)
        {
        }

        protected internal virtual void OnDetachFrom(IEntity parentEntity, object userData)
        {
        }

        protected internal virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }
    }
}
