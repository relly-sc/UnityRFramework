using System.Collections.Generic;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// Unity 实体包装器，将框架生命周期转发给同一对象上的 EntityLogic。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Entity : MonoBehaviour, IEntity
    {
        private readonly List<IEntity> children = new List<IEntity>();
        private EntityLogic logic;

        /// <inheritdoc/>
        public long Id { get; private set; }

        /// <inheritdoc/>
        public EntityStatus Status { get; private set; } = EntityStatus.Unknown;

        /// <inheritdoc/>
        public string AssetName { get; private set; }

        /// <inheritdoc/>
        public object Handle => gameObject;

        /// <inheritdoc/>
        public IEntityGroup Group { get; private set; }

        /// <inheritdoc/>
        public IEntity Parent { get; private set; }

        /// <inheritdoc/>
        public IReadOnlyList<IEntity> Children => children;

        void IEntity.OnInit(long entityId, string assetName, IEntityGroup group, bool isNewInstance,
            object userData)
        {
            Id = entityId;
            AssetName = assetName;
            Group = group;
            Parent = null;
            children.Clear();
            Status = EntityStatus.WillInit;
            logic = GetComponent<EntityLogic>();
            logic?.OnInit(this, isNewInstance, userData);
            Status = EntityStatus.Inited;
        }

        void IEntity.OnRecycle()
        {
            Status = EntityStatus.WillRecycle;
            try
            {
                logic?.OnRecycle();
            }
            finally
            {
                Id = 0;
                AssetName = null;
                Group = null;
                Parent = null;
                children.Clear();
                logic = null;
                Status = EntityStatus.Recycled;
            }
        }

        void IEntity.OnShow(object userData)
        {
            Status = EntityStatus.WillShow;
            gameObject.SetActive(true);
            logic?.OnShow(userData);
            Status = EntityStatus.Showed;
        }

        void IEntity.OnHide(bool isShutdown, object userData)
        {
            Status = EntityStatus.WillHide;
            try
            {
                logic?.OnHide(isShutdown, userData);
            }
            finally
            {
                gameObject.SetActive(false);
                Status = EntityStatus.Hidden;
            }
        }

        void IEntity.OnAttached(IEntity childEntity, object userData)
        {
            if (!children.Contains(childEntity))
            {
                children.Add(childEntity);
            }

            logic?.OnAttached(childEntity, userData);
        }

        void IEntity.OnDetached(IEntity childEntity, object userData)
        {
            children.Remove(childEntity);
            logic?.OnDetached(childEntity, userData);
        }

        void IEntity.OnAttachTo(IEntity parentEntity, object userData)
        {
            Parent = parentEntity;
            if (parentEntity?.Handle is GameObject parentObject)
            {
                transform.SetParent(parentObject.transform, true);
            }

            logic?.OnAttachTo(parentEntity, userData);
        }

        void IEntity.OnDetachFrom(IEntity parentEntity, object userData)
        {
            Parent = null;
            transform.SetParent(null, true);
            logic?.OnDetachFrom(parentEntity, userData);
        }

        void IEntity.OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            logic?.OnUpdate(elapseSeconds, realElapseSeconds);
        }
    }
}
