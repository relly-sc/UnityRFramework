using RFramework.Entity;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 实体逻辑基类，用户继承此类编写实体行为。
    /// 挂载在 Entity 对象的 GameObject 上作为子组件，
    /// 通过 Entity 包装器接收 IEntityModule 的生命周期回调。
    /// </summary>
    /// <remarks>
    /// 设计约束：EntityLogic 是用户扩展点，不是 God Class。
    /// 如果逻辑过多，应拆分为多个独立组件（如 CombatComponent、AnimationComponent）。
    /// </remarks>
    public class EntityLogic : MonoBehaviour
    {
        /// <summary>
        /// 所属的 Entity 包装器引用。
        /// </summary>
        public Entity Owner { get; private set; }

        /// <summary>
        /// 初始化回调。在实体首次创建或从对象池取出时调用。
        /// </summary>
        /// <param name="owner">Entity 包装器实例。</param>
        /// <param name="isNewInstance">是否为新创建的实例。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnInit(Entity owner, bool isNewInstance, object userData)
        {
            Owner = owner;
        }

        /// <summary>
        /// 回收回调。在实体被归还对象池或销毁时调用。
        /// </summary>
        public virtual void OnRecycle()
        {
            Owner = null;
        }

        /// <summary>
        /// 显示回调。在实体可见时调用。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnShow(object userData)
        {
        }

        /// <summary>
        /// 隐藏回调。在实体不可见时调用。
        /// </summary>
        /// <param name="isShutdown">是否为框架关闭。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnHide(bool isShutdown, object userData)
        {
        }

        /// <summary>
        /// 子实体附加回调（作为父实体）。
        /// </summary>
        /// <param name="childEntity">被附加的子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnAttached(IEntity childEntity, object userData)
        {
        }

        /// <summary>
        /// 子实体解除回调（作为父实体）。
        /// </summary>
        /// <param name="childEntity">被解除的子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnDetached(IEntity childEntity, object userData)
        {
        }

        /// <summary>
        /// 附加到父实体回调（作为子实体）。
        /// </summary>
        /// <param name="parentEntity">目标父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnAttachTo(IEntity parentEntity, object userData)
        {
        }

        /// <summary>
        /// 从父实体解除回调（作为子实体）。
        /// </summary>
        /// <param name="parentEntity">原父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnDetachFrom(IEntity parentEntity, object userData)
        {
        }

        /// <summary>
        /// 轮询回调，每帧调用。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间。</param>
        /// <param name="realElapseSeconds">实际流逝时间。</param>
        public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }
    }
}
