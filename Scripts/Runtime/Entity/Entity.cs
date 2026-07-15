using System.Collections.Generic;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 实体 MonoBehaviour 包装器，桥接 IEntity 接口与 Unity GameObject。
    /// 由 IEntityHelper.CreateEntity 创建，持有 EntityLogic 子组件引用，
    /// 将 IEntityModule 的生命周期回调转发给 EntityLogic。
    /// 生命周期方法使用显式接口实现，仅 EntityModule 通过 IEntity 接口调用。
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

        void IEntity.OnInit(long entityId, string assetName, IEntityGroup group, bool isNewInstance, object userData)
        {
            Id = entityId;
            AssetName = assetName;
            Group = group;

            entityLogic = GetComponent<EntityLogic>();
            if (entityLogic == null)
            {
                entityLogic = gameObject.AddComponent<EntityLogic>();
            }

            entityLogic.OnInit(this, isNewInstance, userData);
            Status = EntityStatus.Inited;
        }

        void IEntity.OnRecycle()
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

        void IEntity.OnShow(object userData)
        {
            gameObject.SetActive(true);
            if (entityLogic != null)
            {
                entityLogic.OnShow(userData);
            }

            Status = EntityStatus.Showed;
        }

        void IEntity.OnHide(bool isShutdown, object userData)
        {
            if (entityLogic != null)
            {
                entityLogic.OnHide(isShutdown, userData);
            }

            gameObject.SetActive(false);
            Status = EntityStatus.Hidden;
        }

        void IEntity.OnAttached(IEntity childEntity, object userData)
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

        void IEntity.OnDetached(IEntity childEntity, object userData)
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

        void IEntity.OnAttachTo(IEntity parentEntity, object userData)
        {
            Parent = parentEntity;

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

        void IEntity.OnDetachFrom(IEntity parentEntity, object userData)
        {
            Parent = null;
            transform.SetParent(null);

            if (entityLogic != null)
            {
                entityLogic.OnDetachFrom(parentEntity, userData);
            }
        }

        void IEntity.OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            if (entityLogic != null)
            {
                entityLogic.OnUpdate(elapseSeconds, realElapseSeconds);
            }
        }
    }
}
