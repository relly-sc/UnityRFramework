using System.Collections.Generic;
using RFramework.Entity;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 实体 MonoBehaviour 包装器，桥接 IEntity 接口与 Unity GameObject。
    /// 由 IEntityHelper.CreateEntity 创建，持有 EntityLogic 子组件引用，
    /// 将 IEntityModule 的生命周期回调转发给 EntityLogic。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Entity : MonoBehaviour, IEntity
    {
        /// <summary>
        /// 实体编号。
        /// </summary>
        public long Id { get; private set; }

        /// <summary>
        /// 实体当前状态。
        /// </summary>
        public EntityStatus Status { get; set; } = EntityStatus.Unknown;

        /// <summary>
        /// 实体资源路径。
        /// </summary>
        public string AssetName { get; private set; }

        /// <summary>
        /// 实体实例对象（即当前 GameObject）。
        /// </summary>
        public object Handle => gameObject;

        /// <summary>
        /// 实体所属的实体组。
        /// </summary>
        public IEntityGroup Group { get; private set; }

        /// <summary>
        /// 父实体。
        /// </summary>
        public IEntity Parent { get; private set; }

        /// <summary>
        /// 子实体列表。
        /// </summary>
        private readonly List<IEntity> children = new List<IEntity>();

        /// <summary>
        /// 获取子实体的只读列表。
        /// </summary>
        public IReadOnlyList<IEntity> Children => children;

        /// <summary>
        /// 用户扩展逻辑组件引用。
        /// </summary>
        private EntityLogic entityLogic;

        /// <summary>
        /// 初始化实体。由 EntityModule.InternalShowEntity 调用。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="assetName">实体资源路径。</param>
        /// <param name="group">实体所属组。</param>
        /// <param name="isNewInstance">是否为新创建的实例。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void OnInit(long entityId, string assetName, IEntityGroup group, bool isNewInstance, object userData)
        {
            Id = entityId;
            AssetName = assetName;
            Group = group;

            // 获取或添加 EntityLogic 子组件
            entityLogic = GetComponent<EntityLogic>();
            if (entityLogic == null)
            {
                entityLogic = gameObject.AddComponent<EntityLogic>();
            }

            entityLogic.OnInit(this, isNewInstance, userData);
        }

        /// <summary>
        /// 回收实体。由 EntityModule 回收队列处理。
        /// </summary>
        public void OnRecycle()
        {
            if (entityLogic != null)
            {
                entityLogic.OnRecycle();
            }

            Id = 0;
            AssetName = null;
            Group = null;
            Parent = null;
            children.Clear();
            Status = EntityStatus.Recycled;
        }

        /// <summary>
        /// 显示实体。由 EntityModule.InternalShowEntity 调用。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public void OnShow(object userData)
        {
            gameObject.SetActive(true);
            if (entityLogic != null)
            {
                entityLogic.OnShow(userData);
            }
        }

        /// <summary>
        /// 隐藏实体。由 EntityModule.InternalHideEntity 调用。
        /// </summary>
        /// <param name="isShutdown">是否为框架关闭。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void OnHide(bool isShutdown, object userData)
        {
            if (entityLogic != null)
            {
                entityLogic.OnHide(isShutdown, userData);
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 子实体附加回调。
        /// </summary>
        /// <param name="childEntity">被附加的子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void OnAttached(IEntity childEntity, object userData)
        {
            if (!children.Contains(childEntity))
            {
                children.Add(childEntity);
            }

            if (entityLogic != null)
            {
                entityLogic.OnAttached(childEntity, userData);
            }
        }

        /// <summary>
        /// 子实体解除回调。
        /// </summary>
        /// <param name="childEntity">被解除的子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void OnDetached(IEntity childEntity, object userData)
        {
            if (children.Contains(childEntity))
            {
                children.Remove(childEntity);
            }

            if (entityLogic != null)
            {
                entityLogic.OnDetached(childEntity, userData);
            }
        }

        /// <summary>
        /// 附加到父实体回调。
        /// </summary>
        /// <param name="parentEntity">目标父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void OnAttachTo(IEntity parentEntity, object userData)
        {
            Parent = parentEntity;

            // Unity 层：设置 Transform 父子层级
            Entity parentRuntimeEntity = parentEntity as Entity;
            if (parentRuntimeEntity != null)
            {
                transform.SetParent(parentRuntimeEntity.transform);
            }

            if (entityLogic != null)
            {
                entityLogic.OnAttachTo(parentEntity, userData);
            }
        }

        /// <summary>
        /// 从父实体解除回调。
        /// </summary>
        /// <param name="parentEntity">原父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void OnDetachFrom(IEntity parentEntity, object userData)
        {
            Parent = null;

            // Unity 层：脱离 Transform 父子层级
            transform.SetParent(null);

            if (entityLogic != null)
            {
                entityLogic.OnDetachFrom(parentEntity, userData);
            }
        }

        /// <summary>
        /// 实体轮询回调，每帧调用。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间。</param>
        /// <param name="realElapseSeconds">实际流逝时间。</param>
        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            if (entityLogic != null)
            {
                entityLogic.OnUpdate(elapseSeconds, realElapseSeconds);
            }
        }
    }
}
