using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 实体业务逻辑基类。仅重写实际需要的生命周期方法。
    /// </summary>
    public class EntityLogic : MonoBehaviour
    {
        /// <summary>获取当前显示周期绑定的实体包装器。</summary>
        public Entity Owner { get; private set; }

        /// <summary>为本次显示周期绑定实体包装器。</summary>
        protected internal virtual void OnInit(Entity owner, bool isNewInstance, object userData)
        {
            Owner = owner;
        }

        /// <summary>结束本次显示周期。</summary>
        protected internal virtual void OnRecycle()
        {
            Owner = null;
        }

        /// <summary>实体显示通知。</summary>
        protected internal virtual void OnShow(object userData)
        {
        }

        /// <summary>实体隐藏通知。</summary>
        protected internal virtual void OnHide(bool isShutdown, object userData)
        {
        }

        /// <summary>增加子实体通知。</summary>
        protected internal virtual void OnAttached(IEntity childEntity, object userData)
        {
        }

        /// <summary>移除子实体通知。</summary>
        protected internal virtual void OnDetached(IEntity childEntity, object userData)
        {
        }

        /// <summary>挂到父实体通知。</summary>
        protected internal virtual void OnAttachTo(IEntity parentEntity, object userData)
        {
        }

        /// <summary>脱离父实体通知。</summary>
        protected internal virtual void OnDetachFrom(IEntity parentEntity, object userData)
        {
        }

        /// <summary>实体组逐帧更新通知。</summary>
        protected internal virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }
    }
}
